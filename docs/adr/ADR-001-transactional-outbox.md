# ADR-001 — Transactional Outbox em vez de publish direto

**Status:** Aceito · **Data:** 2026-06-07

## Contexto

Um handler precisa mudar estado local (banco) **e** publicar um evento de
integração (broker). Fazer as duas coisas como passos separados
(`db.Save(); broker.Publish();`) é o anti-padrão **dual-write**: se o processo
morre entre as duas linhas, estado e evento divergem permanentemente. Não há
transação distribuída barata que cubra banco + broker (2PC não escala e o
RabbitMQ não participa de XA de forma prática).

## Decisão

Adotar o **Transactional Outbox**: o evento é gravado numa tabela `outbox` na
**mesma transação ACID local** que persiste a mudança de estado. Um
`OutboxDispatcher` (BackgroundService, polling) lê os pendentes e publica no
RabbitMQ, marcando como processado **somente após** publisher confirm.

## Consequências

- (+) Atomicidade estado+evento sem infraestrutura extra.
- (+) O broker pode estar fora do ar na hora do commit — o evento fica durável no banco e é publicado quando ele volta.
- (−) Latência de publicação = intervalo de polling (mitigável com `LISTEN/NOTIFY` ou intervalo curto).
- (−) Pode publicar duplicado em crash entre publish e marcação → exige Inbox idempotente ([ADR-002](ADR-002-inbox-idempotent-consumer.md)).

## Alternativas rejeitadas

- **Dual-write direto** — simples, mas incorreto sob falha.
- **CDC (Debezium/logical decoding)** — mais robusto em produção, mas adiciona infraestrutura que ofusca a mecânica que este lab quer ensinar.
