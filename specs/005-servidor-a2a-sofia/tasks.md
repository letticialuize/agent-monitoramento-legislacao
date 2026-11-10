# Tarefas: servidor A2A para a SOFIA

**Entrada**: [spec.md](spec.md), [plan.md](plan.md)

Tipo: **A** (automatizável) ou **M** (manual, feita pela responsável).

## Implementação

- [X] T501 [A] Versão A2A e contrato v1 (feature 004, ADR 0009)
- [X] T502 [A] Agent Card publicado (feature 004)
- [X] T503 [A] Parâmetro `contextoNaoPersistido` no chat, sem gravar documentos (RNF01)
- [X] T504 [A] Handler `AnalisarSolicitacaoSofia` e mapeamento de status e achados (RF04, RF06, RF07)
- [X] T505 [A] Endpoint `POST /a2a` com erros JSON-RPC, tempo limite e Task `failed` (RF02, RF03, RF05, RNF02)
- [X] T506 [A] Logs só com ids, status, duração e códigos (RNF03)

## Testes

- [X] T507 [A] Ponta a ponta pela descoberta do card: Task `completed`, schema, ids, links oficiais (critério 1)
- [X] T508 [A] Dois documentos delimitados no LLM e ausentes do Mongo, do Postgres e dos logs (critério 2)
- [X] T509 [A] Injeção de prompt em documento (critério 3)
- [X] T510 [A] LLM indisponível, link inventado e tempo limite (critérios 4 e 5)
- [X] T511 [A] Erros de protocolo e de contrato (-32700, -32600, -32601, -32602)

## Pendências

- [X] T512 [M] Aprovar a emenda 2.1.0 da constitution (Princípio VII e DP-18): aprovada pela responsável em 2026-11-10
- [ ] T513 [M] Definir `A2A__UrlPublica` na Vercel e conferir com curl o card e o `message/send`
- [ ] T514 [M] No repositório da SOFIA: cadastrar o agente no registry e rodar o teste ponta a ponta com ao menos dois agentes
- [ ] T515 [M] Decidir a deduplicação por `idSolicitacao`. Autenticação do A2A resolvida na [feature 006](../006-autenticacao-a2a-sofia/spec.md) (ADR 0010)

## Comandos

```bash
dotnet test tests/Agente/Agente.Integration.Tests --filter "FullyQualifiedName~A2A"
```

```bash
curl -s https://SEU_AGENTE.vercel.app/.well-known/agent-card.json
```

```bash
curl -s -X POST https://SEU_AGENTE.vercel.app/a2a -H "Content-Type: application/json" --data @specs/004-contrato-sofia-a2a/contracts/exemplos/a2a-message-send.json
```
