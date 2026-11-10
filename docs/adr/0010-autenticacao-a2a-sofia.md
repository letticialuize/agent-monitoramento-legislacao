# ADR 0010 — Autenticação A2A com a chave Bearer da SOFIA

- **Status**: Aceito
- **Data**: 2026-11-10
- **Responsável e aprovador**: responsável do projeto
- **Referências**: constitution 2.1.0 (Seção 5.1, DP-13); ADR 0007; ADR 0009; skill `sofia-a2a-autenticacao`; feature 006

## Contexto

A SOFIA chama os agentes do treinamento INFIS/COTI com uma chave Bearer compartilhada, enviada tanto na
descoberta (Agent Card) quanto na execução (RPC). O ADR 0007 deixou a API sem login.

## Decisão

- `GET /.well-known/agent-card.json` e `POST /a2a` exigem `Authorization: Bearer <chave>`, com a chave na
  configuração `SOFIA_A2A_TOKEN` (variável de ambiente na Vercel; user-secrets no ambiente local).
- Chave opaca, comparada exatamente e em tempo constante (hash SHA-256 + `FixedTimeEquals`). Esquema
  `Bearer` sem distinção de maiúsculas; a chave, com distinção. Não é JWT.
- Recusa com 401 e `WWW-Authenticate: Bearer` antes de ler o corpo ou chamar o LLM. Sem chave configurada,
  as duas rotas respondem 503: a integração fica indisponível, nunca aberta.
- Token só no cabeçalho: nunca na URL ou no corpo. A chave não aparece no código, no card, em exemplos
  versionados nem nos logs.
- O card declara `securitySchemes.sofiaService` (`http`/`bearer`) e `security`, sem o segredo.
- 403 não se aplica: a chave é única e não tem níveis de permissão.

## Consequências

- O ADR 0007 deixa de valer para o A2A; as rotas REST de consulta e conversa continuam sem login.
- DP-13 continua pendente para REST e para um mecanismo por usuário: a chave compartilhada não identifica
  o aluno nem o usuário da SOFIA.
- Rotação: troca da variável de ambiente e novo deploy, sem mudança de código.
