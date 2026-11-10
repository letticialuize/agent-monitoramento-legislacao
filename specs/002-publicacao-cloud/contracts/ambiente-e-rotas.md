# Contrato de implantação: ambiente e rotas

## Variáveis de ambiente

Nomes conferidos no código (`IConfiguration` e `IOptions`). O modelo está em [.env.example](../../../.env.example).

| Variável | Obrigatória | Lida em |
|---|---|---|
| `ConnectionStrings__Postgres` | Sim | `DependencyInjection.cs` |
| `ConnectionStrings__Mongo` | Sim | `DependencyInjection.cs` |
| `Llm__ApiKey` | Sim, para o chat | `Llm.cs` |
| `Coleta__AprovadorCatalogoInicial` | Sim, na primeira carga em produção | `CargaCatalogoInicial.cs` |
| `Agendamento__Habilitado` | Sim na Vercel, com valor `false`; o padrão é `true` | `Program.cs` |
| `Agendamento__TokenGatilho` | Sim na Vercel; sem ela, as rotas de tarefas respondem 404 | `TarefasEndpoints.cs` |
| `Cors__AllowedOrigins__N` | Não; o `appsettings.json` traz `https://sofia-infis.onrender.com` | `CorsSofia.cs` (feature 003) |
| `SOFIA_A2A_TOKEN` | Sim, para a integração A2A; sem ela, card e `/a2a` respondem 503 | `AutenticacaoSofia.cs` (feature 006) |
| `A2A__UrlPublica` | Recomendada na Vercel (atrás de proxy); sem ela, o `url` do Agent Card vem do host do pedido | `AgentCard.cs` (feature 004) |
| `PORT` | Não; o padrão é 80 | `Dockerfile.vercel` |
| `OpenTelemetry__Endpoint` | Não | `Observabilidade.cs` |

O nome do banco no Mongo vem de `Memoria:BancoMongo` (`agente`), não da connection string.

## Postgres

- Usar a conexão direta do Neon, sem `-pooler`. `pg_try_advisory_lock` é um bloqueio de sessão, e o
  modo de transação do PgBouncer não o suporta. As migrações rodam na subida e também pedem conexão direta.
- A API cria a extensão `unaccent` ao subir. O usuário do banco precisa ter essa permissão.
- `pgvector` não é necessário: a Api.Agente usa busca textual (ADR 0002).

## Rotas preservadas

| Rota | Uso |
|---|---|
| `GET /saude` | Verificação de saúde |
| `POST /api/v1/consultas` | Consulta estruturada, sem LLM |
| `POST /api/v1/conversas/{conversaId}/mensagens` | Chat, com LLM e memória no Mongo |

O OpenAPI (`/openapi/v1.json`) e o Swagger (`/swagger`) existem em `Development` e, fora dele, só com
`Swagger:Habilitado=true`. O `Dockerfile.vercel` liga essa opção por decisão da responsável (ADR 0007,
atualização de 2026-11-09). O Agent Card A2A v0.3 é público em `GET /.well-known/agent-card.json`
([feature 004](../../004-contrato-sofia-a2a/spec.md)), e o endpoint que ele anuncia, `POST /a2a`
(JSON-RPC `message/send`, `tasks/get`, `tasks/cancel`), está na [feature 005](../../005-servidor-a2a-sofia/spec.md).
Ambos exigem `Authorization: Bearer` com `SOFIA_A2A_TOKEN` (ADR 0010).

As rotas de consulta e conversa aceitam CORS só das origens em `Cors:AllowedOrigins` (política
`SofiaBrowser`, [feature 003](../../003-cors-sofia/spec.md)). As demais rotas não recebem cabeçalhos CORS.

## Rotas novas de operação

Exigem `Authorization: Bearer <Agendamento:TokenGatilho>`, não têm corpo e ficam fora do OpenAPI público.

| Rota | Resposta 200 |
|---|---|
| `POST /api/v1/tarefas/coleta` | `correlacaoId`, `fontes`, `novasNovidades`, `falhas` |
| `POST /api/v1/tarefas/retencao` | `correlacaoId`, `removidosPorTabela` |

As duas respondem 401 sem credencial válida, 404 sem token configurado e 409 com execução em andamento.
