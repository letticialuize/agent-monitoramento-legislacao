# Plano: CORS para o frontend da SOFIA

**Entrada**: [spec.md](spec.md)

## Situação encontrada

- Minimal APIs em `Api.Agente`; sem CORS, sem autenticação (ADR 0007), sem A2A e sem Agent Card.
- Rotas do navegador: grupo `/api/v1` em `ConsultasEndpoints`. `TarefasEndpoints` usa outro grupo
  (`/api/v1/tarefas`), com token próprio (ADR 0008).
- Vercel: `Dockerfile.vercel` já escuta em `0.0.0.0:${PORT}`. O `docker-compose.dev.yml` só sobe os
  bancos; a API roda fora do Compose, então não há rede Docker compartilhada com a SOFIA a ajustar.

## Decisões

| Tema | Decisão |
|---|---|
| Política | Nomeada `SofiaBrowser`, sem política padrão, em `Seguranca/CorsSofia.cs` |
| Escopo | `RequireCorsSofia()` só no grupo de consultas e conversas |
| Métodos e cabeçalhos | `POST`; `Content-Type` e `X-Correlation-Id`; expõe `X-Correlation-Id` |
| Credenciais | Não: as rotas não usam cookie nem token |
| Preflight | Cache de 10 minutos (`Access-Control-Max-Age`) |
| Configuração | `Cors:AllowedOrigins`: produção em `appsettings.json`, local + produção em `appsettings.Development.json`; troca na Vercel por `Cors__AllowedOrigins__N` |
| Validação | Origem inválida derruba a subida; lista vazia não derruba (nenhuma origem liberada) |
| Middleware | `app.UseCors()` logo após a correlação e antes dos endpoints; o roteamento implícito do `WebApplication` já roda antes |

## Arquivos alterados

- `src/Agente/Api.Agente/Seguranca/CorsSofia.cs` (novo)
- `src/Agente/Api.Agente/Program.cs`
- `src/Agente/Api.Agente/Endpoints/ConsultasEndpoints.cs`
- `src/Agente/Api.Agente/appsettings.json`, `appsettings.Development.json`
- `.env.example`, `specs/002-publicacao-cloud/contracts/ambiente-e-rotas.md`
- `tests/Agente/Agente.Integration.Tests/Seguranca/CorsSofiaTests.cs` (novo)

## Riscos

- As rotas continuam abertas (ADR 0007): CORS só controla o que um navegador entrega ao script de outra
  origem. Qualquer cliente fora do navegador continua chamando a API.
- Se a SOFIA mudar de domínio, o navegador bloqueia até a nova origem ser configurada.
