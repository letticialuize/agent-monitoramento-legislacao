# ADR 0001 — Memória conversacional em MongoDB

- **Status**: Aceito
- **Data**: 2026-10-29
- **Referências**: constitution v1.2.0 (Princípio XIV, DP-10); specs/001-monitoramento-boletim-diario/research.md (R-11)

## Contexto

A constitution exige memória conversacional isolada por usuário e conversa, com expiração de 30
dias, limite de histórico enviado ao modelo e separação estrita do conhecimento oficial. A escolha
entre MongoDB e Redis estava pendente (DP-10) e foi decidida pelo responsável do projeto.

## Decisão

Usar MongoDB: coleção `conversas`, índice único (`usuarioId`, `conversaId`), índice TTL de 30 dias
em `atualizadoEm` e leitura limitada às últimas `Memoria:MaxMensagens` mensagens.

## Alternativas

- Redis: expiração nativa por chave, mas persistência e consultas por documento menos adequadas
  ao histórico de conversa. Descartado pela decisão DP-10.

## Consequências

- Nova dependência de infraestrutura (MongoDB) só para memória.
- Nenhuma evidência normativa pode ser lida da memória (Princípio XIV).