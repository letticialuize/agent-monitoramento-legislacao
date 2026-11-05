# ADR 0002 — Busca textual no PostgreSQL, sem RAG, na feature 001

- **Status**: Aceito
- **Data**: 2026-10-29
- **Referências**: research.md (R-04); constitution (Princípios IX, XIII; DP-21, DP-24)

## Contexto

A busca por assunto da feature 001 opera sobre novidades: dados operacionais com retenção de 30
dias. A base de conhecimento do RAG (Api.RAG) é para documentos oficiais versionados.

## Decisão

Guardar metadados, ementa e trecho das novidades no PostgreSQL da Api.Agente, com `tsvector`
(`portuguese` + `unaccent`) e dicionário de sinônimos versionado em `docs/regras/`. A Api.RAG não
participa desta feature. O documento original não é armazenado; guarda-se link oficial e hash.

## Alternativas

- Ingerir novidades na Api.RAG via MCP e usar busca semântica: maior recall para sinônimos, mas
  mistura dado efêmero com conhecimento oficial e exige decidir DP-21.
- Expansão de consulta pelo LLM: pode ser acrescentada depois como indício.

## Consequências

- Busca determinística e explicável; recall depende do dicionário de sinônimos.
- Repositório normativo (DP-24) não é necessário nesta feature.