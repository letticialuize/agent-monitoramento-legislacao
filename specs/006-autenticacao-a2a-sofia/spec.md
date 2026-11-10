# Especificação: autenticação A2A e cadastro na SOFIA

- **Feature**: 006-autenticacao-a2a-sofia
- **Data**: 2026-11-10
- **Origem**: skill `sofia-a2a-autenticacao`; [ADR 0010](../../docs/adr/0010-autenticacao-a2a-sofia.md); features [004](../004-contrato-sofia-a2a/spec.md) e [005](../005-servidor-a2a-sofia/spec.md)

## Requisitos

- **FR-601** `GET /.well-known/agent-card.json` e `POST /a2a` exigem `Authorization: Bearer` com o valor de `SOFIA_A2A_TOKEN`.
- **FR-602** Cabeçalho ausente, esquema diferente de Bearer ou chave incorreta: HTTP 401 com `WWW-Authenticate: Bearer`, antes de ler o corpo ou chamar o LLM.
- **FR-603** `SOFIA_A2A_TOKEN` vazia ou ausente: HTTP 503 nas duas rotas; nunca acesso livre.
- **FR-604** Chave comparada exatamente e em tempo constante; só no cabeçalho; ausente do código, do card, dos exemplos versionados e dos logs.
- **FR-605** Agent Card com `protocolVersion 0.3.0`, `preferredTransport JSONRPC`, modos `text/plain`, skills `monitorar_legislacao_tributaria` e `analisar_documentos`, `securitySchemes.sofiaService` (`http`/`bearer`) e `security`.
- **FR-606** `tasks/get` com `params.id` da Task devolve a mesma Task (id, contextId, status, artefatos) guardada no MongoDB com TTL.
- **FR-607** `tasks/cancel`: Task conhecida → `-32002` (TaskNotCancelable, pois `message/send` é síncrono); desconhecida → `-32001` (TaskNotFound); sem `params.id` → `-32602`.

## Critérios de aceite

| Caso | Resultado |
|---|---|
| Card sem chave ou com chave incorreta | 401 |
| RPC sem chave ou com chave incorreta | 401, sem chamada à OpenAI |
| `SOFIA_A2A_TOKEN` vazia ou ausente | 503 |
| Card autenticado | 200, perfil 0.3.0 com Bearer declarado, sem a chave |
| `message/send` válido | Task, Artifact e ids corretos (feature 005) |
| `tasks/get` | Mesma Task e contexto |
| `tasks/cancel` | -32002 ou -32001 |
| Método inexistente ou payload inválido | Erro JSON-RPC sem stack trace |
| Logs | Sem a chave e sem conteúdo de documento |

## Fora do escopo

- Cadastro na SOFIA (configuração central `A2A:Agents`): feito pelo professor com os dados do [plan.md](plan.md).
- Autenticação das rotas REST (ADR 0007, DP-13).
