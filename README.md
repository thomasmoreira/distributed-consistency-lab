# distributed-consistency-lab

> Consistência entre serviços **sem 2PC** — provada por testes, não por afirmação.

Um lab focado que mostra, com código executável, como garantir que uma operação
de negócio que cruza três serviços chegue a um estado final consistente **mesmo
quando o message broker cai no meio do caminho**.

Os três pilares: **Transactional Outbox** (publicação atômica), **Inbox**
(consumo idempotente) e **Saga** (coordenação com compensação) — implementados
à mão sobre RabbitMQ + PostgreSQL para expor a mecânica que frameworks escondem.

[![CI](https://github.com/thomasmoreira/distributed-consistency-lab/actions/workflows/ci.yml/badge.svg)](https://github.com/thomasmoreira/distributed-consistency-lab/actions/workflows/ci.yml)

---

## A tese

`db.Save(); broker.Publish();` é o anti-padrão **dual-write**: se o processo
morre entre as duas linhas, estado e evento divergem para sempre. Transação
distribuída ACID (2PC) não escala e o RabbitMQ não participa dela na prática.

A resposta de produção é **consistência eventual confiável**:

- O evento é gravado na tabela `outbox` na **mesma transação** que muda o estado → sem dual-write.
- Um dispatcher publica do outbox e só marca como enviado após *publisher confirm*.
- O consumidor é **idempotente** via tabela `inbox` (PK = message-id) → **exactly-once-effect**, mesmo com o at-least-once do broker.
- A Saga coordena o fluxo multi-serviço e **compensa** quando um passo falha.

## Domínio de exemplo — checkout

Pequeno e didático, com compensação natural:

`OrderPlaced` → `ReserveStock` → `ChargePayment` → `OrderConfirmed`
Se o pagamento falha: `PaymentFailed` → `ReleaseStock` (compensação) → `OrderCancelled`

## Arquitetura (C4 — container)

```mermaid
flowchart LR
  Client -->|POST /orders| Orders
  subgraph DB[PostgreSQL — schema por serviço]
    OrdersDB[(orders)]
    InvDB[(inventory)]
    PayDB[(payments)]
  end
  Orders --- OrdersDB
  Inventory --- InvDB
  Payments --- PayDB
  Orders <-->|eventos| MQ((RabbitMQ))
  Inventory <-->|eventos| MQ
  Payments <-->|eventos| MQ
```

| Serviço     | Tipo   | Responsabilidade                                            |
|-------------|--------|------------------------------------------------------------|
| `Orders`    | API    | Cria o pedido; hospeda a `OrderSaga` (orquestração)         |
| `Inventory` | Worker | Reserva / libera estoque                                    |
| `Payments`  | Worker | Cobra / estorna pagamento                                   |

## Failure Modes & Trade-offs

O coração do lab. Cada cenário é um teste de integração que sobe RabbitMQ +
PostgreSQL reais via Testcontainers e — nos casos de broker — **mata e religa**
o container no meio do fluxo.

| #  | Cenário                                              | Garantia provada                                          |
|----|-----------------------------------------------------|----------------------------------------------------------|
| F1 | Broker cai entre o commit do outbox e a publicação  | Nada se perde: o dispatcher republica ao religar         |
| F2 | Mesma mensagem entregue 2× (redelivery)             | Efeito único: o inbox descarta a segunda                 |
| F3 | Consumidor morre após o efeito, antes do ack        | Redelivery + inbox → sem duplicação                      |
| F4 | Pagamento falha no meio da saga                     | Compensação: estoque liberado, pedido cancelado          |
| F5 | Dispatcher crasha entre publish e marcar processed  | Republica (duplicado) → absorvido pelo inbox             |

**Trade-offs assumidos** (ver [`docs/adr/`](docs/adr)):
- Mensageria **caseira** em vez de MassTransit — para expor a mecânica ([ADR-004](docs/adr/ADR-004-handrolled-messaging.md)).
- **Schema por serviço** num único Postgres em vez de banco por serviço ([ADR-005](docs/adr/ADR-005-schema-per-service.md)).

### Em produção eu usaria MassTransit, e por quê

MassTransit já entrega Outbox, retry com backoff, sagas e deduplicação testados
em escala. Reimplementar isso à mão **em produção** seria reinventar a roda. Aqui
foi proposital: o objetivo é entender o que o framework faz por baixo — quando
você sabe a mecânica, sabe configurar e debugar o framework.

## Saga: dois estilos lado a lado

O mesmo checkout aparece em **orquestração** (coordenador central com máquina de
estados persistida, em `src/Services/Orders`) e **coreografia** (reações sem estado
central, em `src/choreography`). Inventory e Payments são idênticos nos dois estilos —
só a coordenação do Orders muda. Comparação em
[ADR-003](docs/adr/ADR-003-saga-orchestration-and-choreography.md) e
[ADR-006](docs/adr/ADR-006-orchestration-vs-choreography.md).

## Como rodar

```bash
# stack local (broker + banco + 3 serviços)
docker compose -f docker/docker-compose.yml up --build

# testes (sobem seus próprios containers via Testcontainers — requer Docker)
dotnet test
```

> Os testes de integração **não** usam o docker-compose: cada teste cria
> containers descartáveis para poder derrubar o broker isoladamente.

## Estrutura

```
src/
  BuildingBlocks/
    Messaging/      IOutbox, IInbox, dispatcher, transporte RabbitMQ
    Persistence/    DbContext base, UoW, entidades outbox/inbox
  Contracts/        eventos de integração versionados
  Services/
    Orders/         API + OrderSaga (orquestração)
    Inventory/      worker
    Payments/       worker
  choreography/     variante de coreografia do Orders (sem estado central)
tests/
  Unit/             máquina de estados da saga
  Integration/      Testcontainers — outbox/inbox, reserva, cobrança, saga,
                    resiliência (F1) + exactly-once E2E, coreografia
docs/adr/           decisões de arquitetura (ADR-001..006)
```

## Decisões de arquitetura

- [ADR-001 — Transactional Outbox](docs/adr/ADR-001-transactional-outbox.md)
- [ADR-002 — Inbox / consumo idempotente](docs/adr/ADR-002-inbox-idempotent-consumer.md)
- [ADR-003 — Saga: orquestração e coreografia](docs/adr/ADR-003-saga-orchestration-and-choreography.md)
- [ADR-004 — Mensageria caseira](docs/adr/ADR-004-handrolled-messaging.md)
- [ADR-005 — Schema por serviço](docs/adr/ADR-005-schema-per-service.md)
- [ADR-006 — Orquestração vs coreografia](docs/adr/ADR-006-orchestration-vs-choreography.md)

---

_Lab de portfólio. Foco: sistemas distribuídos, consistência eventual, design para falha._
