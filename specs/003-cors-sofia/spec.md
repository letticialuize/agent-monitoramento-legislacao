# Especificação: CORS para o frontend da SOFIA

- **Feature**: 003-cors-sofia
- **Data**: 2026-11-10
- **Origem**: skill `sofia-cors-rest-a2a` (SOFIA/INFIS)

## Contexto

A SOFIA tem um frontend no navegador que precisa chamar as rotas REST da Api.Agente. O navegador só
libera a resposta se a API devolver os cabeçalhos CORS para a origem dele. O backend da SOFIA, quando
chama o agente servidor a servidor, não envia `Origin` e não depende de CORS.

Origens informadas pela responsável:

| Ambiente | Origem |
|---|---|
| Local | `http://localhost:4200` |
| Produção | `https://sofia-infis.onrender.com` |

## Fora do escopo

- **Autenticação**: continua desligada nas rotas de consulta e conversa, por decisão da responsável
  (ADR 0007, mantido em 2026-11-10). CORS não substitui autenticação.
- **Servidor A2A e Agent Card**: não existem nesta solução. Ficam para a skill `sofia-a2a-implementacao`.
  CORS não transforma rota REST em servidor A2A.

## Requisitos

- **FR-301** O navegador em origem autorizada consegue chamar `POST /api/v1/consultas` e
  `POST /api/v1/conversas/{conversaId}/mensagens`: o preflight `OPTIONS` devolve
  `Access-Control-Allow-Origin` igual à origem, `POST` nos métodos e `Content-Type` nos cabeçalhos.
- **FR-302** Origem não autorizada não recebe `Access-Control-Allow-Origin`.
- **FR-303** Chamadas sem `Origin` (backend da SOFIA, curl, agendador) seguem o pipeline normal, sem
  cabeçalhos CORS e sem depender deles.
- **FR-304** `/api/v1/tarefas/*`, `/saude`, Swagger e OpenAPI ficam fora da política.
- **FR-305** As origens vêm de `Cors:AllowedOrigins`, por ambiente. Curinga, caminho, barra final ou
  esquema diferente de http/https impedem a subida. Lista vazia: nenhuma origem liberada.
- **FR-306** Sem credenciais no CORS (`Access-Control-Allow-Credentials` ausente).
- **FR-307** O cabeçalho `X-Correlation-Id` pode ser enviado pelo navegador e é exposto na resposta.

## Critérios de aceite

1. Preflight de `http://localhost:4200` e `https://sofia-infis.onrender.com` em consultas e conversas: 204
   com a origem ecoada.
2. Preflight de `https://site-nao-autorizado.example`: sem `Access-Control-Allow-Origin`.
3. `POST /api/v1/consultas` sem `Origin`: 200, sem cabeçalhos CORS.
4. `POST /api/v1/consultas` com `Origin` autorizada: 200, origem ecoada, `Vary: Origin` e
   `X-Correlation-Id` exposto.
5. Preflight em `/api/v1/tarefas/coleta` e `/saude`: sem `Access-Control-Allow-Origin`.
