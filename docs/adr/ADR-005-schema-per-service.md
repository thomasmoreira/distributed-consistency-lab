# ADR-005 — Schema-por-serviço num único PostgreSQL

**Status:** Aceito · **Data:** 2026-06-07

## Contexto

Cada serviço (Orders, Inventory, Payments) deve ser dono do seu estado, sem
acoplamento de tabelas entre eles. O purismo de microsserviços pediria um banco
físico por serviço, o que pesa no `docker-compose` de um lab.

## Decisão

Um único PostgreSQL com **um schema por serviço** (`orders`, `inventory`,
`payments`). Cada serviço só conhece o próprio schema (via `SearchPath` na
connection string) e tem seu próprio `outbox`/`inbox`. Nenhuma query cruza
schemas — comunicação é exclusivamente por eventos.

## Consequências

- (+) Isolamento lógico preservado (cada serviço dono do seu schema).
- (+) `docker-compose` enxuto: um container de banco.
- (+) Caminho de extração para banco dedicado por serviço fica trivial (já não há query cross-schema).
- (−) Não isola falha/recurso a nível físico (aceitável para um lab).

## Alternativas rejeitadas

- **Um banco físico por serviço** — mais fiel a produção, mas adiciona peso operacional sem ganho didático.
- **Schema único compartilhado** — violaria o ownership de dados e o ponto do exercício.
