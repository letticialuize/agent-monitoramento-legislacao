# infis-agente-monitoramento-legislacao

Api.Agente (.NET 10): monitora publicações oficiais de legislação e jurisprudência tributária e responde
consultas sobre as novidades coletadas. Regras do projeto em [.specify/memory/constitution.md](.specify/memory/constitution.md);
especificações em [specs/](specs).

## Rodar localmente

```bash
docker compose -f docker-compose.dev.yml up -d
```

```bash
dotnet run --project src/Agente/Api.Agente --launch-profile http
```

Swagger em `http://localhost:5062/swagger`.

## Testes

```bash
dotnet test AgenteMonitoramentoLegislacao.sln
```

Os testes de integração sobem PostgreSQL e MongoDB com Testcontainers: o Docker precisa estar ligado.

## Integração com a SOFIA (A2A)

| Rota | Uso |
|---|---|
| `GET /.well-known/agent-card.json` | Agent Card A2A v0.3 (descoberta). Exige Bearer |
| `POST /a2a` | JSON-RPC 2.0 `message/send` (contrato SOFIA v1.0), `tasks/get`, `tasks/cancel`. Exige Bearer |
| `POST /api/v1/consultas`, `POST /api/v1/conversas/{id}/mensagens` | REST; CORS liberado só para o frontend da SOFIA |

- Contrato, schemas e exemplos: [specs/004-contrato-sofia-a2a/contracts](specs/004-contrato-sofia-a2a/contracts).
- Servidor A2A: [specs/005-servidor-a2a-sofia](specs/005-servidor-a2a-sofia/spec.md). Decisões: [ADR 0009](docs/adr/0009-contrato-sofia-a2a.md).
- Card e `/a2a` exigem `Authorization: Bearer <CHAVE>`, com a chave em `SOFIA_A2A_TOKEN` ([ADR 0010](docs/adr/0010-autenticacao-a2a-sofia.md)). As rotas REST seguem sem login ([ADR 0007](docs/adr/0007-desvio-api-sem-autenticacao.md)).
- Cadastro na SOFIA: [specs/006-autenticacao-a2a-sofia/plan.md](specs/006-autenticacao-a2a-sofia/plan.md).

Pelo Swagger: clique em **Authorize**, cole só a chave (sem `Bearer`) e use o exemplo de `message/send` já preenchido em `POST /a2a`.

Chave no ambiente local (fica fora do repositório):

```bash
dotnet user-secrets set SOFIA_A2A_TOKEN "<CHAVE>" --project src/Agente/Api.Agente
```

Exemplo com a API local:

```bash
curl -s -X POST http://localhost:5062/a2a -H "Authorization: Bearer <CHAVE>" -H "Content-Type: application/json" --data @specs/004-contrato-sofia-a2a/contracts/exemplos/a2a-message-send.json
```

Configuração (variáveis de ambiente):

| Variável | Uso |
|---|---|
| `SOFIA_A2A_TOKEN` | Chave Bearer da SOFIA. Sem ela, card e `/a2a` respondem 503 |
| `A2A__UrlPublica` | `url` anunciada no Agent Card (ex.: `https://SEU_AGENTE.vercel.app/a2a`) |
| `A2A__TimeoutSegundos` | Tempo máximo de uma análise (padrão 120) |
| `Sofia__IdAgente` | Id do agente no registry da SOFIA (padrão `agente-monitoramento-legislacao`) |
| `Sofia__MaxDocumentos`, `Sofia__MaxCaracteresPorDocumento`, `Sofia__MaxCaracteresTotalDocumentos` | Limites do pedido |
| `Cors__AllowedOrigins__N` | Origens do frontend da SOFIA no navegador |

Demais variáveis: [.env.example](.env.example).
