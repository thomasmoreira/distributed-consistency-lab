# ADR-004 — Mensageria caseira (sem MassTransit/NServiceBus)

**Status:** Aceito · **Data:** 2026-06-07

## Contexto

Frameworks como MassTransit e NServiceBus já oferecem Outbox, retry, sagas e
deduplicação prontos. Em produção, seriam a escolha padrão.

## Decisão

Neste lab, implementar Outbox, Inbox, dispatcher e transporte RabbitMQ **na mão**,
sobre `RabbitMQ.Client` puro. O objetivo é **expor a mecânica** que os frameworks
escondem — é um repositório de estudo/portfólio, não um produto.

O README inclui uma seção explícita: *"Em produção eu usaria MassTransit, e por
quê"*, deixando claro que a decisão é pedagógica, não ingênua.

## Consequências

- (+) O leitor vê exatamente como Outbox/Inbox funcionam por dentro.
- (+) Zero mágica de framework no caminho crítico dos testes de falha.
- (−) Mais código de plumbing para manter (aceitável: é o conteúdo do lab).
- (−) Não cobre recursos avançados (scheduling, deduplicação distribuída) — fora do escopo.

## Alternativas rejeitadas

- **MassTransit** — correto para produção, mas abstrairia justamente o que o lab quer mostrar.
