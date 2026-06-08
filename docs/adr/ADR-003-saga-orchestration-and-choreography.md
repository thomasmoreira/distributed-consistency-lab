# ADR-003 — Entregar a Saga em orquestração E coreografia

**Status:** Aceito · **Data:** 2026-06-07

## Contexto

O checkout cruza três serviços (Orders, Inventory, Payments) e precisa de
**compensação** quando o pagamento falha (liberar o estoque reservado). Há dois
estilos canônicos de Saga e a escolha é um trade-off clássico de arquitetura.

## Decisão

Implementar o **mesmo** fluxo nos **dois** estilos, lado a lado, para que o
leitor compare:

| Aspecto        | Coreografia                                  | Orquestração                                   |
|----------------|----------------------------------------------|------------------------------------------------|
| Coordenação    | Serviços reagem a eventos uns dos outros     | `OrderSaga` central emite comandos e reage     |
| Estado da saga | Implícito (distribuído)                       | Explícito (`saga_state`, máquina de estados)   |
| Vantagem       | Baixo acoplamento; bom para fluxos curtos     | Visível, testável; compensação centralizada    |
| Custo          | Difícil enxergar o fluxo; compensação espalhada | O coordenador vira ponto de complexidade     |

## Consequências

- (+) O valor didático do lab está justamente na comparação direta.
- (+) Demonstra raciocínio de trade-off — o que avaliador sênior procura.
- (−) Dobra a superfície de implementação do fluxo (mitigado: building blocks e contratos são compartilhados).

## Alternativas rejeitadas

- **Escolher só um estilo** — perderia o ponto central do lab.
