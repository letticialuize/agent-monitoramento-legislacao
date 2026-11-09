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

O OpenAPI e o Swagger existem só em `Development`. A2A e Agent Card não existem nesta versão.

## Rotas novas de operação

Exigem `Authorization: Bearer <Agendamento:TokenGatilho>`, não têm corpo e ficam fora do OpenAPI público.

| Rota | Resposta 200 |
|---|---|
| `POST /api/v1/tarefas/coleta` | `correlacaoId`, `fontes`, `novasNovidades`, `falhas` |
| `POST /api/v1/tarefas/retencao` | `correlacaoId`, `removidosPorTabela` |

As duas respondem 401 sem credencial válida, 404 sem token configurado e 409 com execução em andamento.
