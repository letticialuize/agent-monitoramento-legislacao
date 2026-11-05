# Implementation Plan: Monitoramento de novidades e boletim diário

**Branch**: `001-monitoramento-boletim-diario` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-monitoramento-boletim-diario/spec.md`

## Summary

Coletar novidades de legislação e jurisprudência nas fontes do catálogo (federal, nacionais e das
UFs e municípios monitorados), consolidar e classificar cada novidade por situação, esfera e
perfil, e entregá-las de duas formas: consulta sob demanda por assunto (com cruzamento opcional
pelo nicho de um cliente) e boletim diário por e-mail às 8h dos dias úteis.

Abordagem técnica (detalhes em [research.md](research.md)):

- Toda a funcionalidade fica na **Api.Agente**. A Api.RAG não é alterada nesta feature, porque
  novidades são dados operacionais com retenção de 30 dias e a busca por assunto é atendida por
  busca textual no PostgreSQL da Api.Agente (R-04).
- Coleta por **conectores por tipo de fonte** (API, OAI-PMH, página HTML/PDF), executados por um
  agendador interno à Api.Agente, com bloqueio para execução única (R-02, R-03).
- **Validação determinística primeiro**: situação (confirmada/pendente/não catalogada), esfera,
  grupo, deduplicação e perfil por regras versionadas. O LLM só atua em: perfil quando as regras
  não decidem, tema e impacto sugeridos, relação com o nicho e resumo executivo — sempre como
  indício e com saída estruturada validada (R-05, R-06).
- Boletim com **idempotência por dia de referência e lista**, calendário de feriados nacionais
  mantido em tabela e envio por SMTP com padrão outbox (R-07, R-08).

**Entregas** (detalhe e tarefas em [tasks.md](tasks.md), seção "Entregas"):

1. Chat e consulta de novidades classificadas por esfera e perfil (US1) + retenção, consumo e logs.
2. Cadastro de empresa (nome e nicho) e novidades que impactam o nicho (US3).
3. Boletim das 8h e cadastro dos funcionários destinatários (US2).
4. Posterior: fila de revisão humana (US4) e administração de catálogo e entes por API (US5).

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core). Versões exatas de pacotes definidas na
implementação, sempre a última estável compatível com .NET 10.

**Primary Dependencies**: ASP.NET Core (REST e hospedagem do agendador), Semantic Kernel
(conector OpenAI e Function Calling), Npgsql + Entity Framework Core (PostgreSQL), MongoDB C#
Driver (memória conversacional), MailKit (SMTP), AngleSharp (leitura de HTML), PdfPig (texto de
PDF), JsonSchema.Net (validação de contratos JSON), OpenTelemetry (rastreamento e métricas).
Justificativas em [research.md](research.md).

**Storage**: PostgreSQL da Api.Agente (catálogo, entes monitorados, clientes, novidades, consultas,
boletins, destinatários, revisões, consumo e custos, feriados); MongoDB (memória conversacional,
expiração por TTL de 30 dias).

**Testing**: xUnit; Testcontainers (PostgreSQL e MongoDB reais em teste de integração);
WireMock.Net (fontes e OpenAI simuladas); servidor SMTP de teste em contêiner (smtp4dev);
validação de contratos com JSON Schema. Nenhum teste automatizado obrigatório chama o LLM real.

**Target Platform**: Contêiner Linux (ambiente de implantação definido em DP-20).

**Project Type**: Serviço web (API REST) com processamento agendado em segundo plano.

**Performance Goals**: 95% das consultas sob demanda em até 2 minutos (SC-006); boletim entregue
até 8h15 em 95% dos dias úteis (SC-009).

**Constraints**: somente nome e nicho do cliente; nenhum dado operacional por mais de 30 dias
(exceto revisão aberta); nenhuma chamada ao LLM com dados além de novidades públicas, assunto e
nicho; coleta só em destinos do catálogo; segredos fora do código.

**Scale/Scope**: estimativa inicial (a confirmar em DP-20): até 50 usuários internos, até 200
destinatários, catálogo com ~30 fontes nacionais + ~6 fontes por UF/município monitorado, até
1.000 novidades por dia.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como o plano atende | Status |
|---|---|---|
| I. Especificação antes da implementação | Spec 001 aprovada com esclarecimentos; FR ↔ tarefas ↔ testes serão rastreados em tasks.md. | ✅ |
| II. Fontes oficiais | Catálogo versionado no PostgreSQL com carga do PDF; critérios de confiabilidade como validador determinístico; agregadores marcados como apoio (R-03, data-model `Fonte`). | ✅ |
| III. Determinismo antes do LLM | Situação, esfera, grupo, deduplicação, dia de referência, idempotência e perfil por regras são código testável; LLM só como indício (R-05). | ✅ |
| IV. Classificação dos achados | Enum de categoria com as 5 categorias no envelope; falha de fonte = `falha_tecnica`; sem novidades ≠ conformidade (contracts/envelope). | ✅ |
| V. Evidência e rastreabilidade | Toda novidade com link oficial e ocorrências por fonte; `execucaoId` e `correlacaoId` em consultas, coletas, boletins, LLM e logs. | ✅ |
| VI. Fluxo obrigatório | Pipeline: coleta → classificação → cruzamento → validações → LLM → validação da saída → resposta com evidências; etapas registradas em `EtapaExecucao`. | ✅ |
| VII. Minimização de dados | Contratos de cliente com só `nome` e `nicho`; `additionalProperties: false`; nicho de lista fechada. | ✅ |
| VIII. Perfis | Regras versionadas de perfil por fonte e palavras-chave; origem da classificação registrada. | ✅ |
| IX. RAG | Não usado nesta feature (R-04). Nenhuma alteração na Api.RAG. | ✅ (N/A) |
| X. Conteúdo não confiável | Texto coletado vai ao LLM delimitado como dado; testes de injeção (quickstart cenário 9). | ✅ |
| XI. Saída estruturada | 3 contratos JSON versionados para o LLM, Structured Outputs, validação de schema + semântica, até 2 retentativas, envelope preenchido pelo sistema. | ✅ |
| XII. Ferramentas governadas | 3 ferramentas somente leitura com ficha técnica (contracts/ferramentas.md); autorização pelo papel do usuário antes da execução. Hoje com usuário fixo: desvio no ADR 0007. | ⚠️ |
| XIII. Propriedade de dados / MCP | Api.Agente não acessa dados da Api.RAG; nenhuma comunicação com a Api.RAG nesta feature. | ✅ |
| XIV. Memória conversacional | MongoDB, isolamento por usuário e conversa, TTL de 30 dias, limite de mensagens enviadas ao LLM. | ✅ |
| XV. Consumo e custos | `RegistroConsumo` por chamada, incluindo retentativas; valores estimados × informados; desconhecido = nulo + indicador. | ✅ |
| XVI. Revisão humana | Fila de revisão; pendente/não catalogada e indícios da IA marcam revisão. | ✅ |
| XVII. Simplicidade e camadas | Clean Architecture em 4 projetos e DDD por contexto em pastas (justificados em "Arquitetura"); sem MediatR, eventos de domínio ou bases separadas; agendador dentro do mesmo serviço. | ✅ |
| XVIII. Observabilidade | Logs estruturados com IDs, OpenTelemetry, sem segredos e sem dados de cliente além do ID. | ✅ |
| XIX. Entrega | Consulta sob demanda + boletim idempotente, agrupado por esfera e perfil, sem itens não revisados. | ✅ |

Decisões pendentes da constitution tratadas neste plano: DP-14 (modelo
configurável, R-06), DP-16 (retentativas, R-06), DP-17 (tabela de preços, R-10), DP-22 (provedor
de e-mail e gestão de destinatários, R-07), DP-24 (repositório normativo: não usado nesta
feature, R-04), DP-27 (APIs usadas na v1, R-03). Continuam abertas: DP-07, DP-09, DP-18, DP-20,
DP-21, DP-23, DP-25 (valores das listas) e DP-26 (fora do escopo desta feature).

**Resultado do gate inicial**: aprovado, sem violações.

## Project Structure

### Documentation (this feature)

```text
specs/001-monitoramento-boletim-diario/
├── plan.md              # Este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── api-agente.openapi.yaml
│   ├── envelope-resposta.schema.json
│   ├── llm-classificacao-novidade.schema.json
│   ├── llm-relacao-nicho.schema.json
│   ├── llm-resumo-boletim.schema.json
│   ├── ferramentas.md
│   └── boletim-email.md
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
└── Agente/
    ├── Api.Agente/                          # Apresentação + composition root
    │   ├── Endpoints/                       # Um arquivo por contexto (Consultas, Conversas, Revisoes, Boletins, Admin...)
    │   ├── Agendamento/                     # BackgroundServices: só disparam casos de uso
    │   ├── Observabilidade/
    │   └── Program.cs                       # Registro de DI de todas as camadas
    ├── Agente.Application/                  # Casos de uso e portas
    │   ├── Catalogo/                        # ImportarCatalogo, AtivarVersao, ManterEntes
    │   ├── Monitoramento/                   # ColetarFonte, ConsultarNovidades, RevisarNovidade
    │   ├── Clientes/                        # CadastrarCliente, RelacionarNicho
    │   ├── Boletim/                         # GerarBoletim, EnviarBoletim, ManterDestinatarios
    │   ├── Operacao/                        # RegistrarConsumo, AplicarRetencao, RegistradorEtapas
    │   ├── Ferramentas/                     # Funções expostas ao LLM (chamam casos de uso)
    │   ├── Llm/                             # Prompts, contratos e validação da saída
    │   ├── Comum/Envelope/
    │   └── Portas/                          # IConectorFonte, IClienteLlmEstruturado, IEnvioEmail,
    │                                        # IRepositorioConversas, IRelogio, IUnidadeDeTrabalho
    ├── Agente.Domain/                       # Modelo de domínio, sem dependências externas
    │   ├── Comum/                           # Entidade, RaizAgregado, ObjetoDeValor, Resultado, enums
    │   ├── Catalogo/                        # Agregados VersaoCatalogo (com Fontes) e EnteMonitorado
    │   ├── Monitoramento/                   # Agregado Novidade (com Ocorrencias e Revisoes) + serviços de domínio
    │   ├── Clientes/                        # Agregado Cliente; Nicho (lista fechada)
    │   ├── Boletim/                         # Agregados Boletim e ListaDestinatarios; Calendario
    │   └── Operacao/                        # Agregados Execucao (Coleta/Consulta) e RegistroConsumo; TabelaPreco
    └── Agente.Infrastructure/               # Adaptadores das portas
        ├── Persistencia/                    # EF Core: DbContext, configurações, repositórios, migrações
        ├── Memoria/                         # MongoDB
        ├── Conectores/                      # Inlabs, Senado, Camara, LexML, Djen, QueridoDiario, PaginaOficial
        ├── Email/                           # MailKit
        └── Llm/                             # Semantic Kernel / OpenAI

tests/
└── Agente/
    ├── Agente.Domain.Tests/            # Unitários dos validadores e regras
    ├── Agente.Application.Tests/       # Casos de uso com LLM e conectores simulados
    ├── Agente.Contract.Tests/          # JSON Schemas, OpenAPI, ferramentas
    └── Agente.Integration.Tests/       # Testcontainers, WireMock, SMTP de teste

docs/fontes/catalogo-fontes-confiaveis-2026-10-29.pdf   # já existente
```

**Structure Decision**: a Api.Agente segue **Clean Architecture** em 4 projetos (Api, Application,
Domain, Infrastructure) com **DDD** no domínio, organizado por contexto. A alternativa mais simples
— um projeto com pastas — foi descartada porque não impede, em tempo de compilação, que o domínio
dependa de EF Core, Semantic Kernel ou ASP.NET; com projetos separados, a regra do Princípio XVII
vira erro de compilação verificável em PR. A Api.RAG não é criada nesta feature e seguirá a mesma
estrutura quando for especificada. Não há projeto compartilhado entre serviços (evita acoplamento
proibido pelo Princípio XIII). Detalhes na seção a seguir.

## Arquitetura: Clean Architecture e DDD

### Camadas e regra de dependência

```text
        Api.Agente  (apresentação + composition root)
          │     │
          │     ▼
          │   Agente.Infrastructure  (adaptadores: EF Core, MongoDB, conectores, SMTP, OpenAI)
          │     │         │
          ▼     ▼         │ implementa portas e repositórios
        Agente.Application (casos de uso + portas)
                │
                ▼
        Agente.Domain (agregados, objetos de valor, serviços de domínio, contratos de repositório)
```

| Camada | Responsabilidade | PODE depender de | NÃO DEVE |
|---|---|---|---|
| **Domain** | Regras de negócio e invariantes; linguagem ubíqua; validadores determinísticos (Princípio III) | nada além da BCL do .NET | referenciar EF Core, Semantic Kernel, MongoDB, ASP.NET, HTTP, JSON Schema; ter setters públicos em agregados |
| **Application** | Orquestrar o fluxo do Princípio VI; casos de uso; portas para o mundo externo; validação da saída do LLM; montagem do envelope | Domain | conhecer EF Core, SQL, MailKit, SDKs de fontes; conter regra de negócio que pertença a um agregado |
| **Infrastructure** | Implementar portas e repositórios; mapeamento ORM; clientes HTTP; Semantic Kernel | Application, Domain | expor tipos próprios (DbContext, entidades EF separadas, tipos do SK) para a Application |
| **Api** | HTTP, serialização, agendadores, registro de DI | todas (composition root) | conter regra de negócio; acessar `DbContext` direto nos endpoints |

Os agendadores (`BackgroundService`) ficam na Api por serem hospedagem; eles só chamam casos de uso.

### Contextos delimitados (dentro da Api.Agente)

Contextos são **pastas/namespaces** nos projetos, não projetos separados — o volume atual não
justifica módulos implantáveis à parte (Princípio XVII).

| Contexto | Agregados (raiz em negrito) | Serviços de domínio | Observação |
|---|---|---|---|
| **Catálogo** | **VersaoCatalogo** (contém `Fonte`), **EnteMonitorado** | `ValidadorConfiabilidadeFonte` | Fonte só muda por nova versão do catálogo |
| **Monitoramento** | **Novidade** (contém `OcorrenciaNovidade`, `RevisaoNovidade`) | `DeduplicadorNovidades`, `ClassificadorSituacao`, `ResolvedorEsfera`, `ClassificadorPerfilDeterministico` | Máquina de estados da situação dentro do agregado |
| **Clientes** | **Cliente**; `Nicho` como objeto de valor de lista fechada | — | Só nome e nicho (Princípio VII) |
| **Boletim** | **Boletim** (contém `BoletimItem`, `EnvioBoletim`), **ListaDestinatarios** (contém `Destinatario`), **FeriadoNacional** | `CalendarioDiasUteis`, `SeletorItensBoletim` | Idempotência garantida por invariante + restrição única |
| **Operação** | **ExecucaoColeta**, **Consulta** (contém `EtapaExecucao`), **RegistroConsumo**, **TabelaPreco** | — | Contexto de suporte (rastreabilidade e custos) |

Contextos se referenciam **apenas por ID** (ex.: `BoletimItem.NovidadeId`), nunca por navegação
entre agregados.

### Padrões táticos adotados

- **Entidade / Raiz de agregado**: classes base em `Domain/Comum`. Estado alterado só por métodos
  com nome do negócio (`Novidade.Confirmar(linkOficial, revisor, motivo)`, `Boletim.MarcarEnviado(destinatarioId)`),
  que garantem as invariantes do data-model.md; sem setters públicos.
- **Objetos de valor** (imutáveis, validados na criação): `LinkOficial` (URL + domínio oficial),
  `ChaveDeduplicacao`, `Email`, `NomeCliente` (com forma normalizada para unicidade),
  `IntervaloReferencia` (início/fim do boletim), `PeriodoConsulta`, `CodigoFonte`, `Ente` (UF e
  município).
- **Serviços de domínio**: regras que envolvem mais de um objeto ou dados de configuração
  versionada (tabela acima). Puras e testáveis sem infraestrutura.
- **Repositórios**: uma interface **por raiz de agregado** em `Domain/<Contexto>`
  (`IRepositorioNovidades`, `IRepositorioBoletins`…), implementadas na Infrastructure. Consultas
  de leitura (busca textual, fila de revisão) usam **serviços de consulta** na Application
  (`IConsultaNovidades`) implementados na Infrastructure, sem passar pelo agregado.
- **Unidade de trabalho**: `IUnidadeDeTrabalho` na Application, implementada pelo `DbContext`. Regra:
  uma transação altera **um** agregado; exceção permitida para inserção em lote de novas
  `Novidade` independentes numa mesma execução de coleta.
- **Casos de uso**: uma classe por caso de uso na Application (`ConsultarNovidades`,
  `GerarBoletim`…), com entrada e saída próprias; endpoints e agendadores só chamam casos de uso.
  Sem MediatR — chamada direta via DI (menos dependências; Princípio XVII).
- **Portas**: `IConectorFonte`, `IClienteLlmEstruturado`, `IEnvioEmail`, `IRepositorioConversas`,
  `IRelogio` (tempo e fuso injetáveis para testar calendário e 8h), `IRegistradorConsumo`,
  `IBloqueioExecucao` (execução única dos agendadores) e `IExpurgoOperacional` (retenção).
- **Resultado em vez de exceção** para falhas de negócio previsíveis (`Resultado<T>` com erro de
  validação); exceções só para falhas inesperadas, convertidas em `falha_tecnica`.

### Padrões não adotados (e por quê)

- **Eventos de domínio**: nenhum fluxo da v1 precisa reagir de forma assíncrona a mudanças de
  agregado (ex.: "confirmadas após revisão" é obtido por consulta). Adoção futura exige ADR.
- **CQRS com bases separadas**: só a separação lógica entre repositórios (escrita) e serviços de
  consulta (leitura), na mesma base.
- **Entidades de persistência separadas do domínio**: o EF Core mapeia os próprios agregados por
  configuração fluente (campos privados e coleções somente leitura), sem atributos no domínio.

### Como verificar em PR

- Teste de arquitetura (T007 ampliado): `Agente.Domain` sem referências externas;
  `Agente.Application` sem referência a EF Core, MongoDB, MailKit, AngleSharp, PdfPig ou
  Semantic Kernel (exceto abstrações declaradas em ADR); agregados sem setters públicos.
- Revisor rejeita endpoint ou agendador que contenha regra de negócio ou use `DbContext`.
- Toda nova classe de domínio fica na pasta do seu contexto; referência entre contextos só por ID.

## Complexity Tracking

| Violação | Por que é necessária | Alternativa mais simples descartada porque |
|---|---|---|
| API sem autenticação (constitution Seção 5.1; efeitos nos Princípios XII e XIV) — desvio temporário, ADR 0007 | Decisão do responsável do projeto: priorizar a busca; login será implementado depois (T118) | Manter JWT exigiria escolher o provedor de login agora (DP-13) |

## Re-check pós-design (Fase 1)

Após gerar [data-model.md](data-model.md), [contracts/](contracts/) e [quickstart.md](quickstart.md):

- Todos os contratos de cliente têm `additionalProperties: false` e só `nome`/`nicho` (VII). ✅
- O envelope separa campos do sistema (`execucaoId`, `status`, `consumo`, tempos) do conteúdo do
  LLM, e o LLM não recebe nem devolve esses campos (XI). ✅
- Nenhum contrato ou entidade da Api.Agente referencia dados da Api.RAG (XIII). ✅
- Ferramentas expostas ao LLM são somente leitura e a autorização é checada antes da execução
  (XII). ✅
- Estados de `Novidade` impedem chegar a `confirmada` sem link oficial (II, XVI). ✅
- Retenção: todas as entidades operacionais têm data de criação e regra de expurgo; revisão aberta
  bloqueia o expurgo (4.3). ✅
- Agregados e contextos do data-model.md batem com a seção "Arquitetura"; invariantes ficam nos
  agregados e regras determinísticas em serviços de domínio sem dependências externas (III,
  XVII). ✅

**Resultado do gate pós-design**: aprovado.
