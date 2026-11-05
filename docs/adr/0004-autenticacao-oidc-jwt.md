# ADR 0004 — Autenticação OIDC com JWT Bearer

- **Status**: Suspenso pelo ADR 0007 desde 2026-11-05 (autenticação removida temporariamente; reinstalar na T118)
- **Data**: 2026-10-29
- **Referências**: research.md (R-09); constitution (Seção 5.1, DP-13)

## Contexto

Todos os endpoints exigem autenticação e autorização; a memória precisa de identidade de usuário
para isolamento.

## Decisão

JWT Bearer emitido pelo provedor OIDC corporativo, configurado por ambiente (`Autenticacao:*`).
Papéis por claim: `consultor`, `desenvolvedor`, `revisor`, `administrador`; políticas `Consulta`,
`Revisao` e `Administracao`. Ferramentas do LLM herdam a autorização do usuário da conversa.

## Alternativas

- Chave de API: sem identidade de usuário, quebra o isolamento da memória.

## Consequências

- Escolha do provedor real e mapeamento de grupos para papéis ficam em configuração (pendente).