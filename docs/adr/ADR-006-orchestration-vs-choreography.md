# ADR-006 — Saga: orquestração vs coreografia (as duas implementações)

**Status:** Aceito · **Data:** 2026-06-08 · **Relacionado:** [ADR-003](ADR-003-saga-orchestration-and-choreography.md)

## Contexto

O ADR-003 decidiu entregar o **mesmo** checkout nos dois estilos de saga para comparar.
Este ADR registra como cada um ficou no código e o trade-off observado.

Em ambos os estilos, **Inventory e Payments são idênticos** — reagem a eventos de domínio
(`OrderPlaced` → reserva; `StockReserved` → cobra) e emitem o próximo evento via outbox.
A diferença está **só na coordenação do Orders**.

## Decisão

### Orquestração — `Services.Orders.Saga.OrderSagaCoordinator`

- Mantém um **estado de processo persistido** (`saga_state`, uma linha por pedido) e avança
  uma **máquina de estados explícita** (`OrderSaga.Next`).
- Consome todos os eventos de resposta (`StockReserved`, `StockReservationFailed`,
  `PaymentCharged`, `PaymentFailed`, `StockReleased`) — inclusive os intermediários — para
  saber "em que passo estou".
- Transições inválidas lançam: a máquina de estados é a fonte de verdade do progresso.

### Coreografia — `Choreography.ChoreographyCoordinator`

- **Sem** estado de processo e **sem** máquina de estados. Cada reação decide a partir do
  **`Order.Status`** (Pending/Confirmed/Cancelled); o desfecho **emerge** das reações.
- Reage só aos **desfechos** (`PaymentCharged` → confirma; falhas → cancela/compensa) —
  **não** reage a `StockReserved` (não há processo para avançar). Menos consumidores.
- A guarda `Status == Pending` torna cada reação idempotente, sem inbox de saga.

## Trade-off observado

<table header-row="true">
<tr><td>Dimensão</td><td>Orquestração</td><td>Coreografia</td></tr>
<tr><td>Estado do fluxo</td><td>Explícito e consultável (`saga_state`)</td><td>Implícito (emerge do status + eventos)</td></tr>
<tr><td>Acoplamento</td><td>Coordenador central conhece o fluxo todo</td><td>Reações independentes, sem ponto central</td></tr>
<tr><td>Enxergar o processo</td><td>Fácil — uma tabela mostra o passo atual</td><td>Difícil — o fluxo está espalhado pelos handlers</td></tr>
<tr><td>Compensação</td><td>Centralizada na máquina de estados</td><td>Distribuída entre reações</td></tr>
<tr><td>Melhor para</td><td>Fluxos longos/complexos, observabilidade</td><td>Fluxos curtos, baixo acoplamento</td></tr>
</table>

## Consequências

- (+) O leitor compara os dois lado a lado com o resto do sistema (infra, domínio) idêntico.
- (+) Mostra que a escolha é um trade-off real, não dogma.
- (−) Duas coordenações para manter (mitigado: a coreografia é pequena e reusa tudo).

## Como rodar / testar

- Orquestração: caminho padrão em `src/Services/Orders` (provado por `OrderSagaOrchestrationTests`).
- Coreografia: `src/choreography/Choreography` (provado por `ChoreographyCoordinatorTests`),
  com `AddChoreographyOrders()` no lugar dos consumidores de saga.
