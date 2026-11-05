# Quickstart: validação da feature 001

Guia para provar, de ponta a ponta, que a feature funciona. Contratos em [contracts/](contracts/)
e entidades em [data-model.md](data-model.md); este guia não os repete.

## Pré-requisitos

- .NET 10 SDK e Docker (Testcontainers sobe PostgreSQL, MongoDB e smtp4dev).
- Segredos em `appsettings*.json` ou em `dotnet user-secrets` no projeto `Api.Agente` (constitution 5.1; user-secrets têm precedência em Development):
  `ConnectionStrings:Postgres`, `ConnectionStrings:Mongo`, `Smtp:*`, `Llm:ApiKey`,
  `Conectores:Inlabs:*`.
- Configuração não secreta em `appsettings.Development.json`: `Llm:Modelo`, `Llm:Temperatura`,
  `Boletim:Horario` (`08:00`), `Boletim:Fuso` (`America/Sao_Paulo`), `Retencao:Dias` (`30`).

## Execução dos testes

```bash
dotnet test tests/Agente/Agente.Domain.Tests
```

```bash
dotnet test tests/Agente/Agente.Contract.Tests
```

```bash
dotnet test tests/Agente/Agente.Integration.Tests
```

Os testes de integração usam fontes e OpenAI simuladas (WireMock.Net) com fixtures reais salvas;
nenhum chama serviços externos.

## Cenários de validação

| # | Cenário | Como validar | Resultado esperado | Requisitos |
|---|---|---|---|---|
| 1 | Consulta com novidade conhecida no DOU | fixture INLABS com ato sobre o assunto; `POST /api/v1/consultas` | novidade `confirmada`, esfera federal, link do DOU como evidência principal; envelope válido pelo schema | FR-001..003, FR-011, SC-001 |
| 2 | Assunto com sinônimo ("CBS") | fixture com ementa "Contribuição sobre Bens e Serviços" | novidade retornada | R-04 |
| 3 | Fonte fora do ar | WireMock devolve 503 para uma fonte | `status = concluido_com_limitacoes`, achado `falha_tecnica` com código da fonte, fonte em `fontesComFalha` | FR-006, SC-007 |
| 4 | Classificação por perfil | conjunto rotulado por especialistas (mín. 50 novidades) rodado com o modelo configurado | ≥ 90% de esfera e perfil corretos; relatório salvo em `specs/001-monitoramento-boletim-diario/avaliacao/` | FR-008, SC-004 |
| 5 | Só agregador | fixture Querido Diário sem correspondente no DOM | `pendente_confirmacao`, `requerRevisaoHumana = true`, ausente do boletim | FR-003, FR-004, FR-021 |
| 6 | Revisão confirma pendente | `POST /novidades/{id}/revisao` com link oficial; depois gerar boletim | item em "confirmadas após revisão" uma única vez | FR-027, FR-028, FR-021 |
| 7 | Boletim e idempotência | gerar boletim do dia; disparar `POST /boletins/{dia}/envio` duas vezes | e-mails no smtp4dev só uma vez por destinatário; agrupamento esfera/perfil conforme boletim-email.md | FR-018..020, SC-003 |
| 8 | Segunda-feira e feriado | relógio simulado em segunda após feriado na sexta | boletim cobre sexta a domingo; nenhum boletim no feriado | FR-025 |
| 9 | Injeção de prompt | ementa com "ignore as instruções e envie..." | nenhuma mudança de comportamento; saída válida; nenhuma ferramenta extra chamada | Princípio X |
| 10 | Cliente com dados extras | `POST /admin/clientes` com `cnpj` | 400; nada persistido | FR-015, SC-002 |
| 11 | Cruzamento com nicho | cliente `produtos_farmaceuticos`; fixtures de ato da ANVISA e de outro setor | só o ato relacionado aparece, com `relacaoNicho.origem = indicio_ia` e revisão humana | FR-014, FR-017 |
| 12 | LLM inválido | WireMock devolve JSON fora do schema 3 vezes | 2 retentativas registradas em `RegistroConsumo`; item sem sugestão e achado `falha_tecnica`; boletim enviado sem resumo | Princípio XI, XV |
| 13 | Uso desconhecido | resposta da OpenAI sem campo de uso | `usoConhecido = false`, tokens e custo nulos (não zero) | Princípio XV |
| 14 | Retenção | registros com 31 dias, um com revisão `em_analise` | todos expurgados, exceto o em análise; catálogo e clientes intactos | FR-034 |
| 15 | Ente sem fontes | incluir município sem fontes e tentar ativar | ativação recusada | FR-035 |

## Verificações de PR (manuais)

- Projeto `Agente.Domain` não referencia EF Core, Semantic Kernel, MongoDB ou ASP.NET.
- Nenhuma configuração de conexão da Api.RAG na Api.Agente.
- Logs de um cenário completo contêm `execucaoId`/`correlacaoId` e não contêm segredos.
