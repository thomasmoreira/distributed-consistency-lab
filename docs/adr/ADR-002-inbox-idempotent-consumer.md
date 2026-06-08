# ADR-002 — Inbox / consumo idempotente por message-id

**Status:** Aceito · **Data:** 2026-06-07

## Contexto

RabbitMQ entrega **at-least-once**: a mesma mensagem pode chegar mais de uma vez
(redelivery após falha de ack, ou republicação do dispatcher — ver
[ADR-001](ADR-001-transactional-outbox.md)). Sem proteção, um redelivery cobra o
cliente duas vezes ou reserva estoque em dobro.

## Decisão

Cada serviço mantém uma tabela `inbox` com PK = `message_id`. O consumidor, numa
**única transação**, verifica se o id já foi processado; se não, aplica o efeito
de negócio **e** grava o id no `inbox`. A violação de PK funciona como trava de
deduplicação. Resultado: **exactly-once-effect** — o efeito acontece uma vez
ainda que a mensagem chegue N vezes.

## Consequências

- (+) Idempotência explícita, testável e independente do handler.
- (+) Combinada com o Outbox, fecha o ciclo de entrega confiável sem 2PC.
- (−) Toda mensagem custa uma leitura/escrita extra no `inbox`.
- (−) Exige limpeza periódica do `inbox` (retenção) para não crescer indefinidamente.

## Alternativas rejeitadas

- **Deduplicação no broker** — RabbitMQ não oferece dedup nativa confiável.
- **Idempotência ad-hoc no handler** (ex: upsert) — funciona caso a caso, mas não generaliza nem é uniformemente testável.
