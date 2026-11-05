# Research: Monitoramento de novidades e boletim diário

**Feature**: 001-monitoramento-boletim-diario | **Date**: 2026-10-29

Cada item segue o formato Decisão / Justificativa / Alternativas. Itens marcados como "a validar
na implementação" dependem de documentação técnica oficial de terceiros (APIs das fontes) que deve
ser consultada e citada na tarefa correspondente, conforme o Princípio II — este documento não
presume formatos ou endpoints dessas APIs.

## R-01 Estrutura da solução: Clean Architecture + DDD

- **Decisão**: Api.Agente em 4 projetos (Api, Application, Domain, Infrastructure) seguindo Clean
  Architecture, e 4 projetos de teste. Domínio modelado com DDD tático, organizado em 5 contextos
  delimitados como pastas (Catálogo, Monitoramento, Clientes, Boletim, Operação): agregados com
  invariantes, objetos de valor, serviços de domínio e um repositório por raiz de agregado. Casos
  de uso como classes simples na Application, sem MediatR; sem eventos de domínio na v1. Sem
  projeto compartilhado com a Api.RAG. Detalhes em plan.md, seção "Arquitetura".
- **Justificativa**: a regra de dependência fica verificável na compilação (Princípio XVII); o
  domínio puro é onde vivem as regras determinísticas que o LLM não pode substituir (Princípio
  III), testáveis sem infraestrutura; agregados com métodos de negócio garantem estados como
  "confirmada exige link oficial" e "um boletim por dia e lista".
- **Alternativas**: projeto único com pastas (sem garantia de camadas); um projeto por contexto
  delimitado (multiplica projetos sem ganho no volume atual); MediatR e eventos de domínio
  (dependência e indireção sem fluxo que precise deles); projeto "Shared.Contracts" entre serviços
  (acoplamento de versão entre Api.Agente e Api.RAG).

## R-02 Execução agendada (coleta, boletim, retenção)

- **Decisão**: serviços em segundo plano (`BackgroundService`) dentro da Api.Agente, com relógio no
  fuso `America/Sao_Paulo`, e bloqueio consultivo do PostgreSQL (`pg_try_advisory_lock`) para que
  só uma instância execute cada tarefa. Três tarefas: coleta (conforme periodicidade de cada
  fonte), boletim (dias úteis, 8h) e expurgo de retenção (diário, fora do horário comercial). Cada
  execução é registrada em `ExecucaoColeta`/`Boletim` com status.
- **Justificativa**: atende ao volume estimado sem um novo serviço nem biblioteca de agendamento;
  idempotência e bloqueio no banco suportam múltiplas instâncias.
- **Alternativas**: Quartz.NET ou Hangfire (mais recursos, mais dependências e tabelas próprias —
  pode ser adotado depois por ADR se o agendamento crescer); serviço worker separado (novo
  implantável sem necessidade atual).

## R-03 Conectores de coleta e APIs usadas na v1 (resolve DP-27 para esta feature)

- **Decisão**: uma interface de conector por **tipo de acesso**, configurada por fonte no
  catálogo:
  - `Inlabs` — DOU (XML de dados abertos; exige credencial de acesso, guardada como segredo).
  - `SenadoDadosAbertos`, `CamaraDadosAbertos` — APIs de dados abertos.
  - `LexmlOaiPmh` — coleta incremental por OAI-PMH.
  - `Djen` — API de comunicações do DJEN.
  - `QueridoDiario` — API pública, sempre registrada como **agregador de apoio**.
  - `PaginaOficial` — genérico para páginas HTML/PDF de órgãos sem API (SEFAZ, prefeituras,
    tribunais, CARF, CONFAZ, RFB etc.), com regras de extração configuradas por fonte (seletor de
    listagem, padrão de data e número) e versionadas junto ao catálogo.
  - DataJud **fora** desta feature: serve para acompanhar processos, não para descobrir novidades.
- **Justificativa**: reduz código por fonte; fontes novas de uma UF ou município reutilizam o
  conector `PaginaOficial` com configuração, atendendo à FR-035.
- **Alternativas**: um conector por fonte (código demais para dezenas de entes); busca genérica na
  web (viola o Princípio II).
- **A validar na implementação**: endpoints, autenticação, limites de uso e formatos de cada API
  DEVEM ser confirmados na documentação oficial de cada órgão e citados na tarefa do conector.
- **Segurança**: o cliente HTTP só acessa hosts derivados das URLs do catálogo (lista de permissão
  gerada a partir das fontes ativas), com timeout por fonte e retentativa limitada (R-06).

### R-03a Verificação da documentação oficial (2026-10-29, implementação da Entrega 1)

| Conector | Documentação oficial | Situação |
|---|---|---|
| `SenadoDadosAbertos` | https://legis.senado.leg.br/dadosabertos/docs/ (OpenAPI em `/v3/api-docs`) | **Implementado.** `GET /legislacao/lista.json?data=AAAAMMDD` (data de **assinatura**) e `GET /legislacao/{id}.json` para data de publicação (`publicacao[].data`) e `urlDocumento`. Sem autenticação; até 10 req/s. Sem data de publicação no detalhe → usa a assinatura e marca a coleta como parcial. |
| `CamaraDadosAbertos` | https://dadosabertos.camara.leg.br/swagger/api.html | **Implementado.** `GET /proposicoes` com `dataApresentacaoInicio/Fim`, `siglaTipo`, `pagina`, `itens` (máx. 100). Sem autenticação. |
| `Djen` | https://comunicaapi.pje.jus.br/swagger/djen.yml (v1.0.4) | **Implementado, exige configuração.** `GET /comunicacao` com `siglaTribunal` obrigatório na prática (HTTP 422 sem filtro para páginas > 5 itens). Sem `tribunais` na configuração da fonte, a execução é `falha_tecnica`. Limite por IP (`x-ratelimit-*`). |
| `QueridoDiario` | https://docs.queridodiario.ok.org.br/pt-br/latest/utilizando/api-publica.html | **Implementado, exige configuração.** `GET /gazettes` (host funcional: `api.queridodiario.org.br`). Granularidade é o diário inteiro; sempre `agregador_apoio`. Sem `territoryIds` (IBGE), a execução é `falha_tecnica`. |
| `PaginaOficial` | — (configuração por fonte) | **Implementado.** Sem seletores, ou com seletores que não encontram nenhum item (página alterada), a execução é `falha_tecnica`: a fonte não pode parecer consultada sem ter sido (Princípio IV). |
| `Inlabs` (DOU) | https://github.com/Imprensa-Nacional/inlabs (scripts oficiais) | **Bloqueado.** Serviço em manutenção em 2026-10-29, exige conta (login + cookie) e não há dicionário oficial dos campos do XML. Sem conector registrado: cada coleta gera `falha_tecnica` com o motivo. |
| `LexmlOaiPmh` | https://projeto.lexml.gov.br/esquemas/oai_lexml.xsd | **Bloqueado.** Endpoint OAI-PMH público não confirmado (503/404). Mesmo tratamento do INLABS. |

Decisão complementar: o link de um item vindo de fonte oficial do catálogo é tratado como oficial
quando está num host do catálogo **ou** em domínio oficial (`.gov.br`, `.leg.br`, `.jus.br`) — ex.:
`urlDocumento` do Senado em `normas.leg.br`. Link em outro domínio vira `fonte_nao_catalogada`.

### R-03b Configuração das páginas oficiais (verificada nos sites reais em 2026-11-03)

As chaves aceitas estão documentadas em `PaginaOficialConector`. Para repetir a verificação (acessa os
sites reais; fora da suíte automática): `FONTES_REAIS=1`, `FONTES_REAIS_CATALOGO=<catálogo>` e filtro
`VerificacaoFontesReais` no `dotnet test`.

| Situação | Fontes | Observação |
|---|---|---|
| Atos com data e número | `TRIB-CONFAZ`, `TRIB-CGIBS`, `TRIB-NFE` | Convênios, protocolos, atos COTEPE e ajustes (data do DOU na página do ato; só os 15 últimos de cada listagem); resoluções e portarias; notas técnicas e informes. |
| Só notícias e comunicados oficiais | `TRIB-RFB`, `TRIB-PGFN`, `TRIB-MF-CBS`, `TRIB-NFSE`, `TRIB-ESOCIAL`, `TRIB-SPED`, `ADM-CARF`, `JUD-STF`, `JUD-TST`, `JUD-CJF`, `CONTAS-TCU` | **Cobertura parcial** do campo `oQueMonitorar`: súmulas, acórdãos, informativos e soluções de consulta só entram se noticiados. |
| Sem configuração (geram `falha_tecnica`) | `FED-PLANALTO`, `JUD-STJ`, `JUD-TSE`, `TRIB-CGSN`, `JUD-BNP`, `ADM-CGIBS` | Motivos em `pendencias` do catálogo. Bloqueios de acesso automatizado não foram contornados. |

## R-04 Busca por assunto sem RAG nesta feature (resolve DP-24 para esta feature)

- **Decisão**: novidades (metadados, ementa e trecho do texto) ficam no PostgreSQL da Api.Agente
  com índice de busca textual em português (`tsvector` com configuração `portuguese` + extensão
  `unaccent`). A consulta por assunto usa essa busca, com filtros de período, esfera, perfil e
  grupo. O documento original não é armazenado: guarda-se o link oficial e um hash do conteúdo.
  A Api.RAG não participa desta feature; o repositório normativo (DP-24) não é necessário aqui.
- **Justificativa**: novidades são dados operacionais com retenção de 30 dias; colocá-las na base
  de conhecimento do RAG misturaria conhecimento oficial versionado com dado efêmero e exigiria
  decidir DP-21. A busca textual é determinística, explicável e suficiente para ementas.
- **Alternativas**: ingerir novidades na Api.RAG via MCP e busca semântica (maior recall para
  sinônimos, mas acopla retenção e exige DP-21); expansão de consulta pelo LLM (pode ser
  adicionada depois como indício, sem mudar o contrato).
- **Risco**: assuntos com sinônimos ("CBS" × "Contribuição sobre Bens e Serviços") — mitigado por
  dicionário de sinônimos versionado na configuração de busca; medido no quickstart (cenário 2).

## R-05 Regras determinísticas × LLM

- **Decisão**:
  - **Determinístico (domínio)**: critérios de confiabilidade da fonte; situação da novidade;
    esfera, ente e grupo (vêm da fonte); deduplicação; vínculo de retificação quando a fonte
    indica o ato retificado; dia de referência e dias úteis; idempotência do boletim; validação de
    cliente e nicho; perfil por regras versionadas (fonte do grupo de obrigações acessórias →
    desenvolvedor; termos como "leiaute", "schema", "nota técnica", "API" na ementa →
    desenvolvedor; fontes de legislação, tributos e tribunais → consultor tributário). A lista de
    termos é configuração versionada revisada pela equipe, não regra fiscal.
  - **LLM (indício)**: perfil quando as regras não decidem; tema e impacto (alto/médio/baixo)
    sugeridos; relação novidade × nicho com justificativa; resumo executivo do boletim.
- **Justificativa**: Princípio III; o que vem do catálogo é verificável e não precisa do modelo.
- **Alternativas**: classificar tudo pelo LLM (não reproduzível); só regras (não cobre relação
  com nicho e resumo).
- **Deduplicação**: chave natural normalizada (órgão emissor + tipo + número + ano) quando houver
  número; senão hash do texto normalizado da ementa + órgão + data. Ocorrências em outras fontes
  viram `OcorrenciaNovidade`; a publicação com validade jurídica é a evidência principal.

## R-06 Integração com o LLM (resolve DP-14 e DP-16 para esta feature)

- **Decisão**: Semantic Kernel com conector OpenAI; modelo, temperatura e limites de tokens em
  configuração (`Llm:Modelo`, `Llm:Temperatura`, `Llm:MaxTokensSaida`), sem valor fixo no código.
  O modelo escolhido DEVE suportar Structured Outputs com JSON Schema estrito; a escolha concreta é
  feita por ambiente e avaliada com o conjunto de exemplos do quickstart (cenário 4). Cada chamada
  usa um dos contratos em `contracts/llm-*.schema.json`.
- **Validação**: (1) schema; (2) semântica: IDs citados existem na entrada, perfis/impactos estão
  nos enums, resumo só cita novidades listadas, nenhuma URL nova aparece. Falha → até **2
  retentativas** com o erro de validação no prompt; esgotadas, o item fica sem a sugestão e
  marcado `falha_tecnica`, e o restante segue (o boletim não é bloqueado por falha do resumo).
- **Modo estrito**: palavras-chave de JSON Schema não aceitas pelo modo estrito do provedor (ex.:
  limites de tamanho ou de itens) são removidas do schema enviado ao modelo e verificadas na
  validação semântica; o schema completo do repositório continua sendo a referência. A lista exata
  de palavras aceitas DEVE ser conferida na documentação oficial da OpenAI na implementação.
- **Retentativa de rede/limite**: até 3 tentativas com backoff exponencial e jitter para erros
  transitórios (429/5xx/timeout); timeout por chamada configurável.
- **Conteúdo externo**: ementas e trechos vão em bloco delimitado e identificado como dado não
  confiável; o prompt de sistema proíbe seguir instruções do conteúdo (Princípio X).
- **Alternativas**: modo JSON sem schema (validação fraca); biblioteca própria de cliente OpenAI
  (duplica o que o Semantic Kernel já oferece).

## R-07 E-mail e destinatários (resolve parte de DP-22)

- **Decisão**: envio por SMTP corporativo com MailKit, atrás de uma interface `IEnvioEmail`.
  Padrão outbox: o boletim gerado é gravado com status `gerado`; o envio marca `enviado` por
  destinatário; restrição única em (`dia_referencia`, `lista_id`) impede segundo boletim; reenvio
  só para destinatários com falha. Destinatários mantidos por endpoint de administração.
- **Justificativa**: sem dependência de provedor externo específico; idempotência garantida pelo
  banco (FR-020).
- **Alternativas**: SendGrid / Azure Communication Services (podem substituir a implementação de
  `IEnvioEmail` por ADR, sem mudar o domínio).

## R-08 Calendário de dias úteis

- **Decisão**: tabela `FeriadoNacional` mantida por administração (carga anual a partir de fonte
  oficial informada pelo administrador). Se o ano corrente não tiver feriados carregados, o
  boletim é gerado normalmente e um alerta de configuração é registrado e exibido aos
  administradores.
- **Justificativa**: feriados móveis e pontos facultativos mudam por ano; o sistema não deve
  calculá-los nem presumi-los.
- **Alternativas**: biblioteca de feriados (risco de divergência com o calendário oficial).

## R-09 Autenticação e autorização

> **Suspensa em 2026-11-05 (ADR 0007):** a API está sem autenticação. A decisão abaixo volta a valer na tarefa T118; DP-13 segue aberta.

- **Decisão**: JWT Bearer emitido pelo provedor OIDC corporativo (ex.: Microsoft Entra ID),
  configurado por ambiente. Papéis por claim: `consultor`, `desenvolvedor`, `revisor`,
  `administrador`. Políticas: consulta → consultor ou desenvolvedor; revisão → revisor;
  administração → administrador. Ferramentas do LLM herdam a autorização do usuário da conversa.
- **Justificativa**: padrão de mercado, sem armazenar senhas.
- **Alternativas**: chave de API (sem identidade de usuário, quebra o isolamento da memória).
- **Pendente**: escolha do provedor real e mapeamento de grupos → papéis (configuração).

## R-10 Consumo e custos (resolve DP-17 para esta feature)

- **Decisão**: tabela `TabelaPreco` versionada (modelo, preço por milhão de tokens de entrada e
  saída, moeda, vigência, URL da página de preços consultada). Custo estimado = tokens informados
  pelo provedor × preço vigente; se o provedor não informar uso, tokens e custo ficam nulos com
  `uso_conhecido = false`.
- **Justificativa**: Princípio XV.

## R-11 Memória conversacional

- **Decisão**: coleção MongoDB `conversas`, chave (`usuarioId`, `conversaId`); índice TTL de 30
  dias sobre `atualizadoEm`; envio ao LLM limitado às últimas N mensagens (configurável, padrão
  inicial 10, ajustado por avaliação). Só texto da conversa; nenhuma evidência normativa é lida da
  memória.
- **Alternativas**: Redis (descartado pela decisão DP-10).

## R-12 Observabilidade

- **Decisão**: `ILogger` com saída JSON estruturada e OpenTelemetry (traces e métricas), com
  `execucaoId`/`correlacaoId` como atributos. Métricas: novidades por fonte, falhas por fonte,
  duração da coleta, horário de entrega do boletim, chamadas e tokens do LLM.
- **Alternativas**: Serilog (útil, mas não necessário para os requisitos atuais).

## R-13 Testes

- **Decisão**: xUnit; Testcontainers (PostgreSQL, MongoDB); WireMock.Net para fontes e para a API
  da OpenAI; smtp4dev em contêiner; amostras reais de páginas e respostas das fontes salvas como
  fixtures (com data e origem). Testes de contrato validam JSON Schemas e OpenAPI.
- **Justificativa**: Seção 5.2 da constitution.

## R-14 Retenção (Seção 4.3)

- **Decisão**: tarefa diária exclui registros operacionais com mais de 30 dias (consultas,
  execuções, novidades, ocorrências, boletins, consumo); novidade com revisão aberta é mantida até
  a decisão. MongoDB expira por TTL. A retenção de logs é configurada no destino de logs.
- **Observação**: DP-23 (prazo dos registros de custo) continua aberta; a tarefa usa prazo
  configurável com padrão 30 dias.

## Fora do escopo desta feature

- Api.RAG, ingestão de manuais e MCP (próxima feature).
- Servidor A2A.
- Escala de severidade de inconsistências (DP-07).
- Validação determinística de arquivos enviados (DP-09).
