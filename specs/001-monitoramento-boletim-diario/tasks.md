---

description: "Tarefas da feature 001 — Monitoramento de novidades e boletim diário"
---

# Tasks: Monitoramento de novidades e boletim diário

**Input**: Documentos de design em `specs/001-monitoramento-boletim-diario/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Incluídos. A constitution (Seção 5.2 e Definition of Done) exige testes unitários dos
validadores, testes de contrato e testes de integração; os cenários vêm do
[quickstart.md](quickstart.md). Escreva os testes de cada história primeiro e confirme que falham.

**Organization**: Tarefas agrupadas por história de usuário para implementação e teste
independentes.

**Arquitetura**: Clean Architecture + DDD conforme plan.md, seção "Arquitetura". Regras que valem
para todas as tarefas:

- Agregados sem setters públicos; estado muda só por métodos de negócio que garantem as invariantes
  do data-model.md.
- Referência entre agregados e contextos só por ID.
- Uma interface de repositório por raiz de agregado, declarada em `Agente.Domain/<Contexto>/` e
  implementada em `Agente.Infrastructure/Persistencia/Repositorios/`.
- Leituras de listagem/busca usam serviços de consulta (`IConsulta*`) declarados em
  `Agente.Application/<Contexto>/` e implementados em `Agente.Infrastructure/Persistencia/Consultas/`.
- Casos de uso são classes na Application (um por arquivo); endpoints e agendadores só os chamam.
- Falha de negócio previsível retorna `Resultado<T>`; exceção inesperada vira `falha_tecnica`.

> **Autenticação suspensa (ADR 0007, 2026-11-05):** menções a políticas, papéis e respostas 401/403 nas tarefas abertas só valem depois da T118.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história de usuário da spec (US1…US5)

## Path Conventions

Raiz do repositório = `AgenteMonitoramentoLegislacao/`. Código em `src/Agente/<Projeto>/<Contexto>/`,
testes em `tests/Agente/<Projeto>/<Contexto>/`. Contextos: `Catalogo`, `Monitoramento`, `Clientes`,
`Boletim`, `Operacao` (mais `Comum` e, na Application, `Portas`, `Ferramentas`, `Llm`).

---

## Entregas

A feature é entregue em três incrementos, cada um utilizável sozinho:

| Entrega | Escopo | Histórias | Fora desta entrega |
|---|---|---|---|
| **1 — Chat de consulta (MVP)** | Agente pronto para chat e consulta de novidades, com coleta das fontes do catálogo e classificação por perfil (desenvolvedor × consultor tributário) e esfera | US1 | cliente/nicho, e-mail, agendamento das 8h, fila de revisão |
| **2 — Clientes e nichos** | Cadastro de empresa (nome e nicho) e consulta das novidades que impactam o nicho | US3 (+ cadastro de clientes da US5) | e-mail, agendamento |
| **3 — Boletim das 8h** | Agendamento do boletim diário às 8h e cadastro dos funcionários destinatários | US2 (+ destinatários da US5) | — |
| Posterior | Fila de revisão humana; administração de catálogo e entes por API | US4, restante da US5 | — |

Na Entrega 1 o catálogo é carregado pela carga inicial (T040–T041), e novidades pendentes ficam
marcadas com `requerRevisaoHumana = true` (Princípio XVI), mas a fila para confirmá-las chega só
na fase posterior.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialização da solução e registro das decisões arquiteturais

- [X] T001 Criar a solução `AgenteMonitoramentoLegislacao.sln` na raiz com os projetos `src/Agente/Api.Agente` (ASP.NET Core Web API), `src/Agente/Agente.Application`, `src/Agente/Agente.Domain` e `src/Agente/Agente.Infrastructure` (class libraries, `net10.0`), com referências Api→Application+Infrastructure, Infrastructure→Application+Domain, Application→Domain; Domain sem referências; criar as pastas de contexto (`Catalogo`, `Monitoramento`, `Clientes`, `Boletim`, `Operacao`, `Comum`) em Domain e Application, e `Portas`, `Ferramentas`, `Llm` na Application
- [X] T002 Criar os projetos xUnit `tests/Agente/Agente.Domain.Tests`, `Agente.Application.Tests`, `Agente.Contract.Tests` e `Agente.Integration.Tests` e adicioná-los à solução em `AgenteMonitoramentoLegislacao.sln`
- [X] T003 [P] Criar `Directory.Build.props` na raiz com `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true` e analisadores .NET, e `.editorconfig` com convenções C#
- [X] T004 [P] Criar `Directory.Packages.props` (gerenciamento central) com os pacotes do plan.md: Microsoft.SemanticKernel, Npgsql.EntityFrameworkCore.PostgreSQL, MongoDB.Driver, MailKit, AngleSharp, UglyToad.PdfPig, JsonSchema.Net, OpenTelemetry.Extensions.Hosting, Microsoft.AspNetCore.Authentication.JwtBearer, xunit, Testcontainers.PostgreSql, Testcontainers.MongoDb, WireMock.Net — última versão estável compatível com .NET 10
- [X] T005 [P] Criar `docker-compose.dev.yml` na raiz com PostgreSQL, MongoDB e smtp4dev para desenvolvimento local, e `.gitignore` para .NET
- [X] T006 [P] Criar `src/Agente/Api.Agente/appsettings.json` e `appsettings.Development.json` com as chaves não secretas do quickstart (`Llm:Modelo`, `Llm:Temperatura`, `Llm:MaxTokensSaida`, `Boletim:Horario`=`08:00`, `Boletim:Fuso`=`America/Sao_Paulo`, `Retencao:Dias`=`30`, `Memoria:MaxMensagens`=`10`, `Consulta:PeriodoPadraoDias`=`7`, `Consulta:LimiteItens`) e habilitar `dotnet user-secrets` no projeto Api.Agente
- [X] T007 [P] Criar testes de arquitetura em `tests/Agente/Agente.Domain.Tests/ArquiteturaTests.cs` (via `GetReferencedAssemblies` e reflexão, sem pacote extra): (1) `Agente.Domain` não referencia EF Core, Semantic Kernel, MongoDB, ASP.NET, MailKit, AngleSharp, PdfPig nem JsonSchema.Net; (2) `Agente.Application` não referencia EF Core, MongoDB, MailKit, AngleSharp, PdfPig nem Semantic Kernel (exceto abstrações aprovadas em ADR); (3) nenhuma classe que herde `RaizAgregado` ou `Entidade` tem setter público; (4) nenhum tipo de `Api.Agente/Endpoints` ou `Api.Agente/Agendamento` depende de `AgenteDbContext`
- [X] T008 [P] Registrar ADRs em `docs/adr/`: `0001-memoria-conversacional-mongodb.md` (DP-10), `0002-busca-textual-sem-rag-feature-001.md` (R-04), `0003-agendador-interno-api-agente.md` (R-02), `0004-autenticacao-oidc-jwt.md` (R-09), `0005-envio-email-smtp-outbox.md` (R-07), `0006-clean-architecture-ddd.md` (R-01 e plan.md "Arquitetura"), com contexto, decisão, alternativas e consequências

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Blocos de construção DDD, portas, persistência, segurança, observabilidade, envelope,
LLM e catálogo

**⚠️ CRITICAL**: Nenhuma história começa antes desta fase

### Blocos de construção do domínio (Domain/Comum)

- [X] T009 [P] Criar `Entidade`, `RaizAgregado`, `ObjetoDeValor` (igualdade por valor) e `Resultado<T>`/`ErroDominio` em `src/Agente/Agente.Domain/Comum/`, com testes em `tests/Agente/Agente.Domain.Tests/Comum/BlocosDominioTests.cs`
- [X] T010 [P] Criar enums em `src/Agente/Agente.Domain/Comum/Enums.cs`: `Esfera` (`federal`, `estadual`, `municipal`), `EsferaFonte` (`federal`, `estadual`, `municipal`, `nacional`), `Grupo` (`legislacao_federal`, `legislacao_estadual`, `legislacao_municipal`, `tributos`, `tribunais_judiciais`, `administrativos_contas`), `NaturezaFonte` (`oficial`, `agregador_apoio`), `PapelResponsavel` (`AJ`, `AF`, `CC`, `TI`), `Formato` (`html`, `pdf`, `xml`, `json`, `xsd`), `TipoConector` (`Inlabs`, `SenadoDadosAbertos`, `CamaraDadosAbertos`, `LexmlOaiPmh`, `Djen`, `QueridoDiario`, `PaginaOficial`), `Periodicidade` (`diaria_dias_uteis`, `diaria`, `semanal`, `quinzenal`, `trimestral`), `Perfil` (`consultor_tributario`, `desenvolvedor`), `Origem` (`regra_deterministica`, `indicio_ia`), `SituacaoNovidade` (`coletada`, `confirmada`, `pendente_confirmacao`, `fonte_nao_catalogada`, `em_analise`, `descartada`), `Impacto` (`alto`, `medio`, `baixo`), `CategoriaAchado` (`erro_estrutural`, `inconsistencia_negocio`, `indicio_ia`, `ausencia_evidencia`, `falha_tecnica`)

### Contexto Catálogo (domínio)

- [X] T011 [P] Criar objetos de valor `CodigoFonte`, `LinkOficial` (URL absoluta https/http com host extraído; método `EhDominioOficial()` para sufixos `.gov.br`, `.leg.br`, `.jus.br`) e `Ente` (UF de 2 letras e município opcional) em `src/Agente/Agente.Domain/Catalogo/ObjetosDeValor/`, com testes em `tests/Agente/Agente.Domain.Tests/Catalogo/ObjetosDeValorCatalogoTests.cs` (incluir host enganoso `gov.br.exemplo.com`)
- [X] T012 [P] Criar o agregado `VersaoCatalogo` (raiz) com a entidade interna `Fonte` em `src/Agente/Agente.Domain/Catalogo/`: `versao` "única; ex.: `2026-10-29`"; `documentoOrigem` e `aprovadoPor` obrigatórios; método `Ativar()` ("só uma vigente" garantido com o repositório); `AdicionarFonte(...)` garante `codigo` "único por versão", `nome, linkOficial, oQueMonitorar, orgaoEmissor` obrigatórios, `ente` "obrigatório se esfera estadual/municipal", `responsavelInterno` lista
- [X] T013 [P] Criar o serviço de domínio `ValidadorConfiabilidadeFonte` em `src/Agente/Agente.Domain/Catalogo/ValidadorConfiabilidadeFonte.cs`: "`natureza = oficial` exige domínio `.gov.br`, `.leg.br` ou `.jus.br` e `orgaoEmissor`; caso contrário só é aceita como `agregador_apoio`"; exceção de domínio só com aprovador registrado (FR-030, Princípio II); usado por `VersaoCatalogo.AdicionarFonte`
- [X] T014 [P] Testes unitários de T012–T013 em `tests/Agente/Agente.Domain.Tests/Catalogo/VersaoCatalogoTests.cs` (código duplicado, campos obrigatórios, fonte oficial fora do domínio recusada, exceção com e sem aprovador)
- [X] T015 [P] Criar o agregado `EnteMonitorado` em `src/Agente/Agente.Domain/Catalogo/EnteMonitorado.cs`: `tipo` (`uf`, `municipio`), `Ente`, método `Ativar(quantidadeFontesAtivas)` que recusa quando zero — "só pode ser `true` com ao menos uma `Fonte` ativa do ente (FR-035)"; testes em `tests/Agente/Agente.Domain.Tests/Catalogo/EnteMonitoradoTests.cs`
- [X] T016 [P] Declarar `IRepositorioVersoesCatalogo` (obter vigente, obter por versão, adicionar) e `IRepositorioEntesMonitorados` em `src/Agente/Agente.Domain/Catalogo/`

### Contexto Monitoramento (domínio)

- [X] T017 [P] Criar o objeto de valor `ChaveDeduplicacao` em `src/Agente/Agente.Domain/Monitoramento/ChaveDeduplicacao.cs` (forma "órgão emissor + tipo + número + ano" normalizada, ou hash da ementa normalizada + órgão + data quando não há número)
- [X] T018 [P] Criar o agregado `Novidade` (raiz) com entidades internas `OcorrenciaNovidade` e `RevisaoNovidade` em `src/Agente/Agente.Domain/Monitoramento/`: métodos `RegistrarOcorrencia`, `DefinirSituacaoInicial`, `AplicarClassificacao`, `Confirmar(linkOficial, revisor, motivo)`, `Descartar(revisor, motivo)`, `ColocarEmAnalise(revisor, motivo)` implementando a máquina de estados do data-model.md — "`confirmada` exige `linkOficial` de fonte `oficial`; `descartada` não aparece em consultas nem boletins; transição só por revisão humana a partir de pendentes"; `perfis` "ao menos um"; `OcorrenciaNovidade.principal` "exatamente uma principal por novidade"
- [X] T019 [P] Testes unitários do agregado `Novidade` em `tests/Agente/Agente.Domain.Tests/Monitoramento/NovidadeTests.cs` (todas as transições válidas e inválidas, ocorrência principal única, perfis vazios recusados)
- [X] T020 [P] Declarar `IRepositorioNovidades` (obter por id, obter por chave de deduplicação, adicionar em lote, atualizar) em `src/Agente/Agente.Domain/Monitoramento/IRepositorioNovidades.cs`

### Contexto Operação (domínio)

- [X] T021 [P] Criar os agregados `ExecucaoColeta`, `Consulta` (com entidade interna `EtapaExecucao`, etapas `coleta`, `classificacao`, `cruzamento_cliente`, `validacoes`, `ferramentas`, `llm`, `validacao_saida`, `resposta`), `RegistroConsumo` (`tokensEntrada?`, `tokensSaida?`, `usoConhecido` bool, `custoEstimado?`, `custoInformado?`, `tabelaPrecoId?`; fábrica que impede zero quando uso é desconhecido) e `TabelaPreco`, e o objeto de valor `PeriodoConsulta` (início ≤ fim) em `src/Agente/Agente.Domain/Operacao/`, com testes em `tests/Agente/Agente.Domain.Tests/Operacao/OperacaoTests.cs`
- [X] T022 [P] Declarar `IRepositorioExecucoesColeta`, `IRepositorioConsultas`, `IRepositorioConsumo` e `IRepositorioTabelasPreco` em `src/Agente/Agente.Domain/Operacao/`

### Portas da Application

- [X] T023 [P] Declarar as portas em `src/Agente/Agente.Application/Portas/`: `IUnidadeDeTrabalho` (`SalvarAsync`), `IRelogio` (agora em UTC e conversão para `Boletim:Fuso`), `IConectorFonte`, `IClienteLlmEstruturado`, `IEnvioEmail`, `IRepositorioConversas`, `IRegistradorConsumo`, `IBloqueioExecucao` (implementada na Infrastructure com `pg_try_advisory_lock`)

### Persistência (Infrastructure)

- [X] T024 Criar `AgenteDbContext` em `src/Agente/Agente.Infrastructure/Persistencia/AgenteDbContext.cs` e configurações fluentes em `src/Agente/Agente.Infrastructure/Persistencia/Configuracoes/` para os agregados de T012–T022, mapeando campos privados e coleções somente leitura (sem atributos no domínio), objetos de valor como owned types/conversões, índices únicos (`VersaoCatalogo.versao`, `Fonte(versaoCatalogoId, codigo)`, `Novidade.chaveDeduplicacao`), coluna `buscaTexto` tsvector gerada de ementa + tipo + número + órgão com configuração `portuguese` e extensão `unaccent`, e índice GIN
- [X] T025 Implementar `UnidadeDeTrabalho` (sobre o `DbContext`), `BloqueioExecucaoPostgres` (`pg_try_advisory_lock`) e `RelogioSistema` em `src/Agente/Agente.Infrastructure/Persistencia/UnidadeDeTrabalho.cs`, `src/Agente/Agente.Infrastructure/Persistencia/BloqueioExecucaoPostgres.cs` e `src/Agente/Agente.Infrastructure/RelogioSistema.cs`
- [X] T026 Implementar os repositórios de T016, T020 e T022 em `src/Agente/Agente.Infrastructure/Persistencia/Repositorios/` (um arquivo por repositório; `ObterVigente` garante uma só versão vigente)
- [X] T027 Gerar a migração inicial em `src/Agente/Agente.Infrastructure/Persistencia/Migrations/` (depende de T024)
- [X] T028 [P] Criar fixture de integração com Testcontainers (PostgreSQL, MongoDB, smtp4dev), `WebApplicationFactory`, `RelogioFalso` substituindo `IRelogio` e emissão de JWT de teste com papéis em `tests/Agente/Agente.Integration.Tests/Infra/AmbienteTesteFixture.cs`

### Segurança, observabilidade e envelope

- [X] T029 [P] Configurar JWT Bearer e políticas em `src/Agente/Api.Agente/Seguranca/AutorizacaoExtensions.cs`: `Consulta` (papéis `consultor` ou `desenvolvedor`), `Revisao` (`revisor`), `Administracao` (`administrador`); emissor e audiência por configuração (R-09) — **removido em 2026-11-05 (ADR 0007); reinstalar na T118**
- [X] T030 [P] Criar middleware de correlação (`X-Correlation-Id` propagado ou gerado), logs JSON estruturados com `execucaoId`/`correlacaoId` e OpenTelemetry em `src/Agente/Api.Agente/Observabilidade/`; filtro que remove segredos e e-mails de logs
- [X] T031 [P] Criar o envelope e seu construtor em `src/Agente/Agente.Application/Comum/Envelope/` espelhando `contracts/envelope-resposta.schema.json` (status `concluido`, `concluido_com_limitacoes`, `falha_tecnica`, `rejeitado_validacao`; `severidade` sempre `null`); `execucaoId`, tempos e `consumo` preenchidos só pelo sistema; conversão de `Resultado<T>` com erro em `rejeitado_validacao`
- [X] T032 [P] Teste de contrato em `tests/Agente/Agente.Contract.Tests/EnvelopeContractTests.cs` que serializa envelopes de exemplo e valida contra `contracts/envelope-resposta.schema.json` (copiar `specs/001-monitoramento-boletim-diario/contracts/*.json` como arquivos de conteúdo do projeto de teste)
- [X] T033 [P] Criar `RegistradorEtapas` em `src/Agente/Agente.Application/Operacao/RegistradorEtapas.cs`, que registra etapas no agregado `Consulta`/`ExecucaoColeta` para o fluxo do Princípio VI

### LLM e consumo

- [X] T034 Implementar `IClienteLlmEstruturado` com Semantic Kernel/OpenAI em `src/Agente/Agente.Infrastructure/Llm/ClienteLlmSemanticKernel.cs`: modelo e parâmetros de `Llm:*`, Structured Outputs com JSON Schema estrito (remover palavras-chave não aceitas pelo modo estrito conforme documentação oficial da OpenAI), até 3 tentativas com backoff exponencial e jitter para 429/5xx/timeout; nenhum tipo do SK exposto à Application
- [X] T035 Criar `ValidadorSaidaLlm` em `src/Agente/Agente.Application/Llm/ValidadorSaidaLlm.cs`: valida schema com JsonSchema.Net + validação semântica plugável; até 2 retentativas reenviando o erro de validação; esgotadas, retorna `Resultado` com `falha_tecnica` sem lançar exceção (R-06) — JsonSchema.Net fica na Application por ser validação de contrato, registrado no ADR 0006
- [X] T036 Implementar `IRegistradorConsumo` em `src/Agente/Agente.Infrastructure/Llm/RegistradorConsumo.cs`: cria um agregado `RegistroConsumo` por tentativa (incluindo retentativas); sem uso informado → tokens e custo nulos e `usoConhecido = false`; custo estimado pela `TabelaPreco` vigente; nunca registrar zero para desconhecido (Princípio XV)
- [X] T037 [P] Criar `ConteudoNaoConfiavel` e o prompt de sistema base em `src/Agente/Agente.Application/Llm/ConteudoNaoConfiavel.cs`: delimita ementas e trechos como dado e proíbe seguir instruções neles (Princípio X)
- [X] T038 [P] Testes unitários de T035 e T036 em `tests/Agente/Agente.Application.Tests/Llm/` (schema inválido 3 vezes → 2 retentativas e `falha_tecnica`; uso ausente → nulos)
- [X] T039 [P] Criar simulador da API da OpenAI com WireMock.Net em `tests/Agente/Agente.Integration.Tests/Infra/OpenAiSimulado.cs` (respostas válidas, inválidas, sem uso, 429)

### Catálogo inicial e conectores base

- [X] T040 Transcrever o catálogo para `docs/fontes/catalogo-2026-10-29.json` a partir de `docs/fontes/catalogo-fontes-confiaveis-2026-10-29.pdf`: uma entrada por fonte nacional/federal com os campos de `FonteInput` (contracts/api-agente.openapi.yaml); links extraídos das anotações do PDF; campo sem valor no PDF fica ausente e marcado em `pendencias` — nunca inventado; fontes com `<UF>`/`<Municipio>` ficam como modelos, sem instância
- [X] T041 Criar o caso de uso `ImportarCatalogo` em `src/Agente/Agente.Application/Catalogo/ImportarCatalogo.cs`: cria o agregado `VersaoCatalogo` a partir do JSON via `AdicionarFonte` (que aplica `ValidadorConfiabilidadeFonte`) e recusa a versão inteira se alguma fonte for inválida; persiste com `IRepositorioVersoesCatalogo` + `IUnidadeDeTrabalho`; ativação é caso de uso separado (`AtivarVersaoCatalogo`, mesmo diretório)
- [X] T042 Implementar o handler HTTP de lista de permissão e a base dos conectores em `src/Agente/Agente.Infrastructure/Conectores/`: hosts permitidos derivados das fontes ativas do catálogo vigente, timeout por fonte, retentativa limitada; destino fora da lista → erro `falha_tecnica`
- [X] T043 [P] Testes do handler de lista de permissão em `tests/Agente/Agente.Integration.Tests/Conectores/ListaPermissaoTests.cs`

**Checkpoint**: Fundação pronta — Entrega 1 pode começar

---

## Phase 3: Entrega 1 — User Story 1 - Chat e consulta de novidades (Priority: P1) 🎯 MVP

**Goal**: Agente pronto para chat e consulta por assunto, com novidades coletadas das fontes do
catálogo, classificadas por esfera e por perfil (desenvolvedor × consultor tributário), com
evidências e limitações. Sem cliente, sem e-mail, sem agendamento das 8h.

**Independent Test**: Com fixtures de fontes, `POST /api/v1/consultas` e
`POST /api/v1/conversas/{id}/mensagens` retornam as novidades esperadas, classificadas e com link
oficial; assunto sem novidades retorna "sem novidades encontradas" com fontes consultadas
(quickstart cenários 1, 2, 3, 5, 9).

### Tests for User Story 1 ⚠️

- [X] T044 [P] [US1] Teste de integração do quickstart cenário 1 (ato no DOU → `confirmada`, esfera federal, link do DOU como evidência principal, envelope válido) em `tests/Agente/Agente.Integration.Tests/Monitoramento/ConsultaDouTests.cs` — implementado com a fonte oficial do Senado no lugar do DOU, enquanto T056 está bloqueada.
- [X] T045 [P] [US1] Teste de integração do cenário 2 (sinônimo "CBS") em `tests/Agente/Agente.Integration.Tests/Monitoramento/ConsultaSinonimoTests.cs`
- [X] T046 [P] [US1] Teste de integração do cenário 3 (fonte 503 → `concluido_com_limitacoes`, achado `falha_tecnica`, fonte em `fontesComFalha`) em `tests/Agente/Agente.Integration.Tests/Monitoramento/FonteIndisponivelTests.cs`
- [X] T047 [P] [US1] Teste de integração do cenário 5 (só agregador → `pendente_confirmacao`, `requerRevisaoHumana = true`) e de fonte fora do catálogo (`fonte_nao_catalogada`) em `tests/Agente/Agente.Integration.Tests/Monitoramento/SituacaoNovidadeTests.cs`
- [X] T048 [P] [US1] Teste de integração do cenário 9 (injeção de prompt na ementa) em `tests/Agente/Agente.Integration.Tests/Monitoramento/InjecaoPromptTests.cs`
- [X] T049 [P] [US1] Teste de integração de assunto sem novidades (FR-012: mensagem "sem novidades encontradas", fontes consultadas, sem afirmar ausência de mudança normativa) e de corte por limite (`itensOmitidos > 0`) em `tests/Agente/Agente.Integration.Tests/Monitoramento/ConsultaSemResultadoTests.cs`
- [X] T050 [P] [US1] Teste de contrato de `ConsultaRequest` (campos extras rejeitados, `assunto` minLength 2 maxLength 200) e da resposta 200 contra o envelope em `tests/Agente/Agente.Contract.Tests/ConsultasContractTests.cs`

### Implementation for User Story 1

- [X] T051 [P] [US1] Criar o serviço de domínio `DeduplicadorNovidades` em `src/Agente/Agente.Domain/Monitoramento/DeduplicadorNovidades.cs`: gera `ChaveDeduplicacao`; ocorrências adicionais viram `OcorrenciaNovidade` do agregado existente; publicação com validade jurídica vira principal; testes em `tests/Agente/Agente.Domain.Tests/Monitoramento/DeduplicadorNovidadesTests.cs`
- [X] T052 [P] [US1] Criar o serviço de domínio `ClassificadorSituacao` em `src/Agente/Agente.Domain/Monitoramento/ClassificadorSituacao.cs` (fonte `oficial` do catálogo → `confirmada`; só `agregador_apoio` → `pendente_confirmacao`; sem fonte do catálogo → `fonte_nao_catalogada`) com testes em `tests/Agente/Agente.Domain.Tests/Monitoramento/ClassificadorSituacaoTests.cs`
- [X] T053 [P] [US1] Criar o serviço de domínio `ResolvedorEsfera` em `src/Agente/Agente.Domain/Monitoramento/ResolvedorEsfera.cs`: herda esfera da fonte; para fonte `nacional`, usa a tabela versionada `docs/regras/esfera-por-orgao-v1.json` (órgão emissor → esfera, ente) recebida como parâmetro (o domínio não lê arquivo); órgão não mapeado → esfera padrão da fonte e limitação registrada; testes em `tests/Agente/Agente.Domain.Tests/Monitoramento/ResolvedorEsferaTests.cs`
- [X] T054 [P] [US1] Criar o serviço de domínio `ClassificadorPerfilDeterministico` em `src/Agente/Agente.Domain/Monitoramento/ClassificadorPerfilDeterministico.cs` com regras de `docs/regras/perfis-v1.json` recebidas como parâmetro (fonte de obrigações acessórias → `desenvolvedor`; termos "leiaute", "layout", "schema", "nota técnica", "API" na ementa → `desenvolvedor`; grupos de legislação, tributos e tribunais → `consultor_tributario`); retorna "indefinido" quando nenhuma regra decide; testes em `tests/Agente/Agente.Domain.Tests/Monitoramento/ClassificadorPerfilTests.cs`
- [X] T055 [P] [US1] Criar `ProvedorRegrasVersionadas` em `src/Agente/Agente.Infrastructure/Regras/ProvedorRegrasVersionadas.cs`, que lê `docs/regras/*.json` (perfis, esfera por órgão, sinônimos) com versão e entrega objetos de configuração ao domínio e à busca; criar os três arquivos JSON iniciais em `docs/regras/`
- [ ] T056 [P] [US1] Implementar conector `Inlabs` (DOU) em `src/Agente/Agente.Infrastructure/Conectores/InlabsConector.cs` com credencial via segredo; formato e endpoints conforme documentação oficial do INLABS citada no PR; fixture real em `tests/Agente/Agente.Integration.Tests/Fixtures/Inlabs/` — **BLOQUEADA (2026-10-29)**: INLABS em manutenção, exige conta e não há dicionário oficial dos campos do XML (research R-03a). Sem conector, a fonte FED-DOU gera `falha_tecnica` em cada coleta.
- [X] T057 [P] [US1] Implementar conector genérico `PaginaOficial` em `src/Agente/Agente.Infrastructure/Conectores/PaginaOficialConector.cs` (AngleSharp para HTML, PdfPig para PDF) usando `configuracaoConector` da fonte (seletor de listagem, padrões de data e número); fixtures em `tests/Agente/Agente.Integration.Tests/Fixtures/PaginaOficial/`
- [X] T058 [P] [US1] Implementar conector `QueridoDiario` em `src/Agente/Agente.Infrastructure/Conectores/QueridoDiarioConector.cs`, sempre marcando ocorrências como `agregador_apoio`; documentação oficial citada no PR
- [X] T059 [P] [US1] Implementar conectores `SenadoDadosAbertos` e `CamaraDadosAbertos` em `src/Agente/Agente.Infrastructure/Conectores/SenadoConector.cs` e `CamaraConector.cs`; documentação oficial citada no PR; fixtures
- [ ] T060 [P] [US1] Implementar conector `LexmlOaiPmh` em `src/Agente/Agente.Infrastructure/Conectores/LexmlConector.cs` (coleta incremental OAI-PMH); documentação oficial citada no PR; fixtures — **BLOQUEADA (2026-10-29)**: endpoint OAI-PMH público do LexML não confirmado (503/404; research R-03a).
- [X] T061 [P] [US1] Implementar conector `Djen` em `src/Agente/Agente.Infrastructure/Conectores/DjenConector.cs`; documentação oficial do CNJ citada no PR; fixtures
- [X] T062 [US1] Criar o caso de uso `ColetarFonte` em `src/Agente/Agente.Application/Monitoramento/ColetarFonte.cs`: cria `ExecucaoColeta`, chama `IConectorFonte`, aplica T051–T054, adiciona/atualiza agregados `Novidade` via `IRepositorioNovidades` (inserção em lote permitida), registra falha como `falha_tecnica` sem tratar como ausência de novidades (FR-001..009)
- [X] T063 [US1] Criar o caso de uso `ClassificarNovidadesComLlm` em `src/Agente/Agente.Application/Monitoramento/ClassificarNovidadesComLlm.cs` usando `contracts/llm-classificacao-novidade.schema.json` só para novidades com perfil indefinido ou sem tema/impacto; validação semântica "cada novidadeId deve existir na entrada"; aplica via `Novidade.AplicarClassificacao` com origem `indicio_ia`; falha → novidade sem sugestão + achado `falha_tecnica`
- [X] T064 [US1] Criar `AgendadorColeta` (`BackgroundService`) em `src/Agente/Api.Agente/Agendamento/AgendadorColeta.cs`: usa `IRelogio`, periodicidade de cada fonte, `IBloqueioExecucao` para execução única; só chama `ColetarFonte` e `ClassificarNovidadesComLlm`
- [X] T065 [US1] Declarar `IConsultaNovidades` em `src/Agente/Agente.Application/Monitoramento/IConsultaNovidades.cs` e implementar em `src/Agente/Agente.Infrastructure/Persistencia/Consultas/ConsultaNovidades.cs`: busca textual em `buscaTexto` com `unaccent`, expansão por sinônimos (T055), filtros de período, esfera, perfil, grupo e situação; exclui `descartada`; retorna DTOs de leitura
- [X] T066 [US1] Criar o caso de uso `ConsultarNovidades` em `src/Agente/Agente.Application/Monitoramento/ConsultarNovidades.cs`: período padrão de `Consulta:PeriodoPadraoDias` via `PeriodoConsulta`, limite de itens com `itensOmitidos`, fontes consultadas e com falha, `mensagemSemNovidades`, `requerRevisaoHumana` pelos critérios do Princípio XVI, achados, agregado `Consulta` com etapas; monta o envelope (FR-010..013)
- [X] T067 [US1] Criar endpoint `POST /api/v1/consultas` em `src/Agente/Api.Agente/Endpoints/ConsultasEndpoints.cs` com política `Consulta`, validação (problem+json 400) e chamada única a `ConsultarNovidades`
- [X] T068 [US1] Implementar `IRepositorioConversas` em `src/Agente/Agente.Infrastructure/Memoria/RepositorioConversas.cs`: coleção `conversas`, índice único (`usuarioId`, `conversaId`), índice TTL de 30 dias em `atualizadoEm`, leitura limitada a `Memoria:MaxMensagens`
- [X] T069 [US1] Criar ferramentas `buscar_novidades` e `obter_novidade` em `src/Agente/Agente.Application/Ferramentas/FerramentasNovidades.cs` conforme `contracts/ferramentas.md`, chamando `ConsultarNovidades`/`IConsultaNovidades`: autorização do papel antes de executar, validação de argumentos, timeout configurável (padrão 30 s), etapa `ferramentas`; o registro como funções do Semantic Kernel fica em `src/Agente/Agente.Infrastructure/Llm/RegistroFerramentasSemanticKernel.cs`
- [X] T070 [US1] Criar o caso de uso `ResponderMensagemConversa` em `src/Agente/Agente.Application/Monitoramento/ResponderMensagemConversa.cs` (isolamento pelo usuário do token, validação da resposta: só cita ids e URLs retornados pelas ferramentas, envelope) e o endpoint `POST /api/v1/conversas/{conversaId}/mensagens` em `src/Agente/Api.Agente/Endpoints/ConversasEndpoints.cs`
- [X] T071 [P] [US1] Testes das ferramentas (papel ausente recusado, argumentos inválidos, descartada não retornada) em `tests/Agente/Agente.Application.Tests/Ferramentas/FerramentasNovidadesTests.cs`

---

## Phase 4: Fechamento da Entrega 1 (transversal)

**Purpose**: Requisitos da constitution que valem desde a primeira entrega (retenção, consumo e
logs)

- [X] T072 Criar o caso de uso `AplicarRetencao` em `src/Agente/Agente.Application/Operacao/AplicarRetencao.cs` (porta `IExpurgoOperacional` implementada em `src/Agente/Agente.Infrastructure/Persistencia/ExpurgoOperacional.cs`) e `AgendadorRetencao` em `src/Agente/Api.Agente/Agendamento/AgendadorRetencao.cs`: exclui registros operacionais com mais de `Retencao:Dias` (na Entrega 1: consultas, execuções, etapas, novidades, ocorrências, consumo; boletins, itens e envios entram na Entrega 3), mantém novidade `em_analise`, não toca catálogo, entes, clientes, destinatários e feriados; a memória conversacional expira pelo TTL do MongoDB (FR-034)
- [X] T073 [P] Teste de integração do cenário 14 (retenção) em `tests/Agente/Agente.Integration.Tests/Operacao/RetencaoTests.cs`
- [X] T074 [P] Teste de integração do cenário 13 (uso desconhecido → `usoConhecido = false`, tokens e custo nulos) em `tests/Agente/Agente.Integration.Tests/Operacao/ConsumoDesconhecidoTests.cs`
- [X] T075 [P] Teste que roda um fluxo completo e verifica que os logs contêm `execucaoId`/`correlacaoId` e não contêm chave de API nem credencial de fonte (a Entrega 3 amplia o teste para senha SMTP e e-mails de destinatários) em `tests/Agente/Agente.Integration.Tests/Operacao/LogsTests.cs`

**Checkpoint**: Entrega 1 pronta para uso — validar quickstart cenários 1, 2, 3, 5, 9, 13, 14

---

## Phase 5: Entrega 2 — User Story 3 - Clientes, nichos e novidades que os impactam (Priority: P2)

**Goal**: Cadastrar empresa (somente nome e nicho) e consultar, no chat ou na API, as novidades que
impactam o nicho, com justificativa marcada como indício.

**Independent Test**: Cadastrar cliente `produtos_farmaceuticos` por `POST /api/v1/admin/clientes`;
consulta com `clienteNome` retorna só o ato relacionado, com `relacaoNicho.origem = indicio_ia` e
revisão humana; cadastro com dados extras é recusado (quickstart cenários 10 e 11).

### Tests for User Story 3 ⚠️

- [ ] T076 [P] [US3] Teste de integração do cenário 11 em `tests/Agente/Agente.Integration.Tests/Clientes/ConsultaClienteTests.cs`
- [ ] T077 [P] [US3] Testes de integração: cliente inexistente → 404 sem busca; campos de cliente além de `clienteNome` na consulta → 400; LLM recebe só código e descrição do nicho (inspecionar requisição no WireMock) em `tests/Agente/Agente.Integration.Tests/Clientes/MinimizacaoDadosClienteTests.cs`
- [ ] T078 [P] [US3] Teste de integração do cenário 10 (cliente com `cnpj` → 400, nada persistido) em `tests/Agente/Agente.Integration.Tests/Clientes/ClientesAdminTests.cs`

### Implementation for User Story 3

- [ ] T079 [P] [US3] Criar os objetos de valor `NomeCliente` (forma normalizada sem distinção de maiúsculas/acentos) e `Nicho` (códigos `varejo_mercadorias`, `produtos_farmaceuticos`, `tributacao_padrao`; lista fechada) e o agregado `Cliente` ("nenhum outro campo"; `nichoCodigo` "deve existir e estar ativo") em `src/Agente/Agente.Domain/Clientes/`, com `IRepositorioClientes`; testes em `tests/Agente/Agente.Domain.Tests/Clientes/ClienteTests.cs`
- [ ] T080 [US3] Mapear `Cliente` e a tabela de nichos no `AgenteDbContext` com índice único do nome normalizado e carga dos três nichos; implementar `RepositorioClientes` em `src/Agente/Agente.Infrastructure/Persistencia/Repositorios/RepositorioClientes.cs`; nova migração em `src/Agente/Agente.Infrastructure/Persistencia/Migrations/`
- [ ] T081 [US3] Criar o caso de uso `CadastrarCliente` em `src/Agente/Agente.Application/Clientes/CadastrarCliente.cs` e os endpoints `GET`/`POST /api/v1/admin/clientes` em `src/Agente/Api.Agente/Endpoints/ClientesAdminEndpoints.cs` com `ClienteInput` (`additionalProperties: false`, `nome` minLength 2 maxLength 200, `nicho` da lista fechada) — depende de T079–T080
- [ ] T082 [US3] Criar o caso de uso `RelacionarNicho` em `src/Agente/Agente.Application/Clientes/RelacionarNicho.cs` com `contracts/llm-relacao-nicho.schema.json`: entrada só nicho (código e descrição) e novidades (id, ementa, órgão, tipo); validação "novidadeId existe na entrada"; resultado `indicio_ia` com revisão humana
- [ ] T083 [US3] Estender `ConsultarNovidades` em `src/Agente/Agente.Application/Monitoramento/Consulta.cs` para `clienteNome`: resolve o cliente por `IRepositorioClientes` (404 se inexistente), etapa `cruzamento_cliente`, chama `RelacionarNicho`, filtra por `relacionada = true`, preenche `cliente` e `relacaoNicho` (FR-014..017)
- [ ] T084 [US3] Adicionar a ferramenta `buscar_novidades_cliente` em `src/Agente/Agente.Application/Ferramentas/FerramentasNovidades.cs` conforme `contracts/ferramentas.md`, com testes em `tests/Agente/Agente.Application.Tests/Ferramentas/FerramentasNovidadesTests.cs`

**Checkpoint**: Entregas 1 e 2 funcionam

---

## Phase 6: Entrega 3 — User Story 2 - Boletim das 8h e cadastro de funcionários (Priority: P3)

**Goal**: Cadastrar os funcionários destinatários e enviar em dias úteis, às 8h, o boletim com as
novidades confirmadas do período, agrupado por esfera e perfil, sem duplicidade.

**Independent Test**: Cadastrar destinatários pela API, gerar e enviar o boletim com
`RelogioFalso`; conferir conteúdo no smtp4dev e que um segundo disparo não reenvia (quickstart
cenários 7, 8, 12).

### Tests for User Story 2 ⚠️

- [ ] T085 [P] [US2] Teste de integração do cenário 7 (agrupamento esfera/perfil, idempotência com dois disparos) em `tests/Agente/Agente.Integration.Tests/Boletim/BoletimIdempotenciaTests.cs`
- [ ] T086 [P] [US2] Teste de integração do cenário 8 com `RelogioFalso` (segunda-feira após feriado na sexta cobre sexta a domingo; nenhum boletim no feriado) em `tests/Agente/Agente.Integration.Tests/Boletim/BoletimCalendarioTests.cs`
- [ ] T087 [P] [US2] Teste de integração do cenário 12 aplicado ao resumo (LLM inválido → boletim enviado sem resumo, retentativas em `RegistroConsumo`) em `tests/Agente/Agente.Integration.Tests/Boletim/BoletimResumoFalhaTests.cs`
- [ ] T088 [P] [US2] Testes de integração: dia sem novidades (FR-022), fonte diária com falha listada (FR-023), pendentes excluídos (FR-021), novidade coletada após o envio não entra em boletim posterior (FR-025), falha SMTP mantém boletim não enviado (US2 cenário 8) em `tests/Agente/Agente.Integration.Tests/Boletim/BoletimRegrasTests.cs`
- [ ] T089 [P] [US2] Teste de contrato do e-mail (estrutura e ordem de seções de `contracts/boletim-email.md`, ementa codificada como texto, nenhum item não confirmado) em `tests/Agente/Agente.Contract.Tests/BoletimEmailContractTests.cs`
- [ ] T090 [P] [US2] Testes de integração de destinatários (incluir, e-mail duplicado na lista → 400, desativado não recebe o próximo boletim) em `tests/Agente/Agente.Integration.Tests/Boletim/DestinatariosAdminTests.cs`

### Implementation for User Story 2

- [ ] T091 [P] [US2] Criar o objeto de valor `IntervaloReferencia` e o agregado `FeriadoNacional` (`data` única, `descricao`, `fonteInformada`, `cadastradoPor`) e o serviço de domínio `CalendarioDiasUteis` (dias úteis = seg–sex exceto feriados nacionais; cálculo de `diaReferenciaInicio`..`diaReferenciaFim`) em `src/Agente/Agente.Domain/Boletim/`; testes em `tests/Agente/Agente.Domain.Tests/Boletim/CalendarioDiasUteisTests.cs`
- [ ] T092 [P] [US2] Criar o objeto de valor `Email` e o agregado `ListaDestinatarios` com entidade interna `Destinatario` (`nome`, `Email`, `ativo`; métodos `Incluir`, `Ativar`, `Desativar`; e-mail "único por lista, válido") em `src/Agente/Agente.Domain/Boletim/`; testes em `tests/Agente/Agente.Domain.Tests/Boletim/ListaDestinatariosTests.cs`
- [ ] T093 [P] [US2] Criar o agregado `Boletim` com entidades internas `BoletimItem` (`secao` `confirmadas` ou `confirmadas_apos_revisao`, referência a novidade por ID) e `EnvioBoletim` (`status` `pendente`, `enviado`, `falha`; tentativas; último erro) em `src/Agente/Agente.Domain/Boletim/`: `status` (`gerado`, `enviando`, `enviado`, `enviado_parcial`, `falha_tecnica`) calculado pelos envios; métodos `MarcarEnviado`, `MarcarFalha`, `DestinatariosAReenviar`; testes em `tests/Agente/Agente.Domain.Tests/Boletim/BoletimTests.cs`
- [ ] T094 [P] [US2] Declarar `IRepositorioBoletins` (obter por lista e `diaReferenciaFim`, último boletim da lista, adicionar), `IRepositorioListasDestinatarios` e `IRepositorioFeriados` em `src/Agente/Agente.Domain/Boletim/`
- [ ] T095 [US2] Mapear T091–T093 no `AgenteDbContext` (configurações em `src/Agente/Agente.Infrastructure/Persistencia/Configuracoes/`) com as restrições "única (`listaId`, `diaReferenciaFim`)" em `Boletim`, "única por (`boletimId`, `destinatarioId`)" em `EnvioBoletim`, "uma novidade aparece no máximo uma vez em seção `confirmadas_apos_revisao`" e `FeriadoNacional.data` única; implementar os repositórios de T094 em `src/Agente/Agente.Infrastructure/Persistencia/Repositorios/`; incluir `Boletim`, `BoletimItem` e `EnvioBoletim` no `ExpurgoOperacional` (T072) e ampliar o teste de logs (T075) para senha SMTP e e-mails; nova migração em `src/Agente/Agente.Infrastructure/Persistencia/Migrations/`
- [ ] T096 [US2] Criar o caso de uso `ManterDestinatarios` em `src/Agente/Agente.Application/Boletim/ManterDestinatarios.cs` e os endpoints `GET`/`POST /api/v1/admin/destinatarios` e `PATCH /api/v1/admin/destinatarios/{id}` em `src/Agente/Api.Agente/Endpoints/DestinatariosAdminEndpoints.cs` (somente `nome`, `email`, `ativo`) — depende de T092 e T095
- [ ] T097 [US2] Criar o serviço de domínio `SeletorItensBoletim` em `src/Agente/Agente.Domain/Boletim/SeletorItensBoletim.cs` (recebe novidades candidatas e o `IntervaloReferencia`): só `confirmada` com `dataPublicacao` no intervalo e coletada antes da geração; confirmadas por revisão depois do boletim do seu dia vão para `confirmadas_apos_revisao` uma única vez; testes em `tests/Agente/Agente.Domain.Tests/Boletim/SeletorItensBoletimTests.cs`
- [ ] T098 [US2] Declarar `IConsultaCandidatosBoletim` em `src/Agente/Agente.Application/Boletim/IConsultaCandidatosBoletim.cs` e implementar em `src/Agente/Agente.Infrastructure/Persistencia/Consultas/ConsultaCandidatosBoletim.cs` (novidades do intervalo e confirmadas por revisão ainda não publicadas em boletim; fontes diárias com falha no intervalo)
- [ ] T099 [US2] Criar `GeradorResumoBoletim` em `src/Agente/Agente.Application/Boletim/GeradorResumoBoletim.cs` com `contracts/llm-resumo-boletim.schema.json`; validação semântica "todo id citado em destaques existe na entrada; o texto não contém URLs"; falha → resumo nulo
- [ ] T100 [P] [US2] Criar `RenderizadorEmailBoletim` em `src/Agente/Agente.Infrastructure/Email/RenderizadorEmailBoletim.cs` (HTML + texto puro, assunto e seções de `contracts/boletim-email.md`, codificação de conteúdo coletado)
- [ ] T101 [P] [US2] Implementar `IEnvioEmail` com MailKit em `src/Agente/Agente.Infrastructure/Email/SmtpEnvioEmail.cs` (configuração `Smtp:*` via segredo)
- [ ] T102 [US2] Criar o caso de uso `GerarBoletim` em `src/Agente/Agente.Application/Boletim/GerarBoletim.cs`: calcula o intervalo com `CalendarioDiasUteis` e `IRelogio`, seleciona itens com `SeletorItensBoletim`, gera resumo, cria o agregado `Boletim` com `fontesNaoVerificadas`, `semNovidades` e um `EnvioBoletim` pendente por destinatário ativo; retorna o existente se já gerado (FR-018..024)
- [ ] T103 [US2] Criar o caso de uso `EnviarBoletim` em `src/Agente/Agente.Application/Boletim/EnviarBoletim.cs`: envia só para `DestinatariosAReenviar`, atualiza o agregado por destinatário; sem aprovação prévia (FR-019, FR-020, FR-026)
- [ ] T104 [US2] Criar `AgendadorBoletim` (`BackgroundService`) em `src/Agente/Api.Agente/Agendamento/AgendadorBoletim.cs`: dias úteis às `Boletim:Horario` via `IRelogio`, execução única por `IBloqueioExecucao`, chama `GerarBoletim` e `EnviarBoletim`; alerta de configuração quando o ano corrente não tiver feriados carregados (R-08)
- [ ] T105 [US2] Criar endpoints `GET /api/v1/boletins/{diaReferenciaFim}` e `POST /api/v1/boletins/{diaReferenciaFim}/envio` (200 quando já enviado a todos; 202 quando retomado) em `src/Agente/Api.Agente/Endpoints/BoletinsEndpoints.cs`
- [ ] T106 [US2] Criar o caso de uso `CarregarFeriados` em `src/Agente/Agente.Application/Boletim/CarregarFeriados.cs` e o endpoint `POST /api/v1/admin/feriados` em `src/Agente/Api.Agente/Endpoints/FeriadosEndpoints.cs` (política `Administracao`, `fonteInformada` obrigatória)

**Checkpoint**: Entregas 1, 2 e 3 funcionam

---

## Phase 7: Posterior — User Story 4 - Revisão humana de novidades não confirmadas (Priority: P4)

**Goal**: Revisores confirmam (com link oficial), descartam ou mantêm em análise novidades
pendentes.

**Independent Test**: Novidade de fonte não catalogada confirmada com link oficial passa a
aparecer como confirmada na consulta e no boletim seguinte (quickstart cenário 6).

### Tests for User Story 4 ⚠️

- [ ] T107 [P] [US4] Teste de integração do cenário 6 (confirmar → consulta e "confirmadas após revisão" no boletim seguinte, uma única vez) em `tests/Agente/Agente.Integration.Tests/Monitoramento/RevisaoConfirmaTests.cs`
- [ ] T108 [P] [US4] Testes de integração: confirmação sem link ou com link fora de fonte oficial do catálogo → 400; descarte remove de consultas e boletins com motivo registrado; revisão de novidade já confirmada → 409; papel diferente de `revisor` → 403 em `tests/Agente/Agente.Integration.Tests/Monitoramento/RevisaoRegrasTests.cs`

### Implementation for User Story 4

- [ ] T109 [US4] Criar o caso de uso `RevisarNovidade` em `src/Agente/Agente.Application/Monitoramento/RevisarNovidade.cs`: carrega o agregado `Novidade`, verifica com `IRepositorioVersoesCatalogo` que o host do `LinkOficial` pertence a fonte `oficial` ativa, chama `Confirmar`/`Descartar`/`ColocarEmAnalise` e salva com `IUnidadeDeTrabalho` (FR-027, FR-028)
- [ ] T110 [US4] Criar a consulta de fila `IConsultaPendentesRevisao` em `src/Agente/Agente.Application/Monitoramento/IConsultaPendentesRevisao.cs` (implementação em `src/Agente/Agente.Infrastructure/Persistencia/Consultas/ConsultaPendentesRevisao.cs`) e os endpoints `GET /api/v1/novidades/pendentes` e `POST /api/v1/novidades/{novidadeId}/revisao` em `src/Agente/Api.Agente/Endpoints/RevisoesEndpoints.cs` (política `Revisao`)

---

## Phase 8: Posterior — User Story 5 - Administração de catálogo e entes por API (Priority: P5)

**Goal**: Administradores importam e ativam versões do catálogo e mantêm UFs e municípios
monitorados por API (clientes e destinatários já entregues nas Entregas 2 e 3).

**Independent Test**: Importar o catálogo de 2026-10-29 pela API, ativá-lo e recusar ente sem
fontes (quickstart cenário 15).

### Tests for User Story 5 ⚠️

- [ ] T111 [P] [US5] Teste de integração do cenário 15 (ativar município sem fontes → recusado) em `tests/Agente/Agente.Integration.Tests/Catalogo/EntesMonitoradosTests.cs`
- [ ] T112 [P] [US5] Testes de integração do catálogo: importação de `docs/fontes/catalogo-2026-10-29.json` cria versão com campos da seção 4.4; fonte sem domínio oficial como `oficial` recusa a versão; ativação troca a versão vigente em `tests/Agente/Agente.Integration.Tests/Catalogo/CatalogoAdminTests.cs`

### Implementation for User Story 5

- [ ] T113 [US5] Criar endpoints `POST /api/v1/admin/catalogo/versoes` e `POST /api/v1/admin/catalogo/versoes/{versao}/ativacao` em `src/Agente/Api.Agente/Endpoints/CatalogoAdminEndpoints.cs` chamando `ImportarCatalogo` e `AtivarVersaoCatalogo` (política `Administracao`)
- [ ] T114 [US5] Criar o caso de uso `ManterEntesMonitorados` em `src/Agente/Agente.Application/Catalogo/ManterEntesMonitorados.cs` (conta fontes ativas do ente na versão vigente e chama `EnteMonitorado.Ativar`) e os endpoints `GET`/`POST /api/v1/admin/entes-monitorados` e ativação em `src/Agente/Api.Agente/Endpoints/EntesMonitoradosEndpoints.cs` (FR-035)

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Avaliação, documentação e validação final

- [ ] T115 [P] Criar harness de avaliação do cenário 4 em `tests/Agente/Agente.Integration.Tests/Avaliacao/AvaliacaoClassificacaoTests.cs` (categoria `Avaliacao`, fora da suíte padrão) que lê `specs/001-monitoramento-boletim-diario/avaliacao/conjunto-rotulado.json` (fornecido pelos especialistas) e grava o relatório de acerto de esfera e perfil (SC-004)
- [ ] T116 [P] Criar `docs/README-execucao.md` com execução local (docker-compose, user-secrets, migrações, carga do catálogo e feriados) e um resumo das camadas e contextos com link para plan.md "Arquitetura"
- [ ] T117 Executar todos os cenários do `specs/001-monitoramento-boletim-diario/quickstart.md`, os testes de arquitetura (T007) e as verificações manuais de PR, registrando o resultado no PR
- [ ] T118 Reinstalar a autenticação conforme ADR 0004 e encerrar o desvio do ADR 0007: JWT Bearer e políticas em `src/Agente/Api.Agente/Seguranca/AutorizacaoExtensions.cs`, trocar o usuário fixo `sem-login` de `src/Agente/Api.Agente/Endpoints/ConsultasEndpoints.cs` pelo usuário do token e recolocar os testes de 401 e 403 — depende da escolha do provedor (DP-13)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)**: bloqueia todas as entregas
- **Entrega 1 (Phases 3–4)**: depende da Fase 2
- **Entrega 2 (Phase 5)**: depende da Entrega 1 (`ConsultarNovidades` T066 e ferramentas T069)
- **Entrega 3 (Phase 6)**: depende da Fase 2; nos testes usa novidades semeadas, mas em produção precisa da coleta da Entrega 1
- **Posterior (Phases 7–8)**: dependem da Fase 2; o cenário 6 completo da US4 usa o boletim (Entrega 3)
- **Polish (Phase 9)**: ao final

### Within Each Phase (ordem DDD)

- Testes primeiro, falhando
- Objetos de valor → agregados → serviços de domínio → interfaces de repositório (Domain)
- Mapeamento EF e repositórios → migração (Infrastructure)
- Casos de uso e serviços de consulta (Application)
- Endpoints e agendadores (Api)
- Migrações em sequência: T027 → T080 → T095 (não gerar em paralelo)
- T024 depende de T012–T022; T026 depende de T024
- T062 depende de T051–T061; T066 depende de T065; T070 depende de T068–T069
- T072 cobre as entidades operacionais existentes na Entrega 1; Entregas 2 e 3 DEVEM incluir suas entidades operacionais no expurgo
- T081 depende de T079–T080; T083 depende de T066 e T082
- T096 depende de T092 e T095; T102 depende de T091–T099; T103 depende de T101–T102; T104 e T105 dependem de T102–T103

### Parallel Opportunities

- Setup: T003–T008
- Fundação: T009–T022 (arquivos por contexto); T028–T033; T037–T039
- Entrega 1: testes T044–T050; serviços de domínio T051–T054; T055; conectores T056–T061; T073–T075
- Entrega 2: T076, T077, T078, T079
- Entrega 3: testes T085–T089 e T090; domínio T091–T094; T100–T101

---

## Parallel Example: Entrega 1 (User Story 1)

```bash
# Testes juntos:
Task: "T044 cenário 1 em tests/Agente/Agente.Integration.Tests/Monitoramento/ConsultaDouTests.cs"
Task: "T046 cenário 3 em tests/Agente/Agente.Integration.Tests/Monitoramento/FonteIndisponivelTests.cs"
Task: "T047 cenário 5 em tests/Agente/Agente.Integration.Tests/Monitoramento/SituacaoNovidadeTests.cs"

# Serviços de domínio do contexto Monitoramento juntos:
Task: "T051 DeduplicadorNovidades" / "T052 ClassificadorSituacao" / "T053 ResolvedorEsfera" / "T054 ClassificadorPerfilDeterministico"

# Conectores juntos:
Task: "T056 Inlabs" / "T057 PaginaOficial" / "T058 QueridoDiario" / "T059 Senado e Câmara" / "T060 LexML" / "T061 DJEN"
```

---

## Implementation Strategy

### Entrega 1 (MVP)

1. Fase 1 (Setup) e Fase 2 (Fundação)
2. Fase 3 (US1) — pode começar só com T056 (DOU), T057 (páginas oficiais) e T058 (Querido Diário), acrescentando os demais conectores depois
3. Fase 4 (retenção, consumo, logs)
4. **Parar e validar**: quickstart cenários 1, 2, 3, 5, 9, 13, 14 e testes de arquitetura
5. Entregar o chat de consulta

### Entregas seguintes

1. Entrega 2: Fase 5 → validar cenários 10 e 11
2. Entrega 3: Fase 6 → validar cenários 7, 8 e 12
3. Posterior: Fases 7 e 8, depois Fase 9 (avaliação SC-004 e quickstart completo)

---

## Notes

- Toda tarefa de conector DEVE citar, no PR, a documentação oficial da API ou página usada (Princípio II); formatos não presumidos
- Regras versionadas em `docs/regras/` são configuração revisada pela equipe, não regra fiscal; o domínio as recebe como parâmetro, sem ler arquivos
- [P] = arquivos diferentes, sem dependência pendente
- Commit após cada tarefa ou grupo lógico; parar em qualquer checkpoint para validar a entrega
