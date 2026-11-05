# Data Model: Monitoramento de novidades e boletim diário

**Feature**: 001-monitoramento-boletim-diario | **Date**: 2026-10-29

Todas as entidades pertencem à Api.Agente (PostgreSQL, exceto `Conversa`, em MongoDB). Campos
`criadoEm`/`atualizadoEm` (UTC) existem em todas e não são repetidos abaixo. "Operacional" indica
retenção de 30 dias (Seção 4.3 da constitution).

**Mapa DDD** (ver plan.md, seção "Arquitetura"): raízes de agregado e o que cada uma contém.

| Contexto | Raiz de agregado | Entidades internas | Objetos de valor |
|---|---|---|---|
| Catálogo | `VersaoCatalogo` | `Fonte` | `CodigoFonte`, `LinkOficial`, `Ente` |
| Catálogo | `EnteMonitorado` | — | `Ente` |
| Monitoramento | `Novidade` | `OcorrenciaNovidade`, `RevisaoNovidade` | `ChaveDeduplicacao`, `LinkOficial`, `Ente` |
| Clientes | `Cliente` | — | `NomeCliente`, `Nicho` |
| Boletim | `Boletim` | `BoletimItem`, `EnvioBoletim` | `IntervaloReferencia` |
| Boletim | `ListaDestinatarios` | `Destinatario` | `Email` |
| Boletim | `FeriadoNacional` | — | — |
| Operação | `ExecucaoColeta` | — | — |
| Operação | `Consulta` | `EtapaExecucao` | `PeriodoConsulta` |
| Operação | `RegistroConsumo` | — | — |
| Operação | `TabelaPreco` | — | — |

Referências entre agregados são só por ID (ex.: `BoletimItem.novidadeId`, `OcorrenciaNovidade.fonteId`).
`Nicho` é tabela de lista fechada no banco, mas no domínio é objeto de valor.

## Catálogo e abrangência

### VersaoCatalogo
| Campo | Tipo | Regras |
|---|---|---|
| id | int | |
| versao | texto | única; ex.: `2026-10-29` |
| documentoOrigem | texto | caminho no repositório, ex.: `docs/fontes/catalogo-fontes-confiaveis-2026-10-29.pdf` |
| aprovadoPor | texto | obrigatório |
| vigente | bool | só uma vigente |

### Fonte
| Campo | Tipo | Regras |
|---|---|---|
| id | uuid | |
| versaoCatalogoId | FK | |
| codigo | texto | único por versão (ex.: `FED-DOU`, `TRIB-SEFAZ-SP`) |
| nome, linkOficial, oQueMonitorar, orgaoEmissor | texto | obrigatórios |
| esfera | enum | `federal`, `estadual`, `municipal`, `nacional` (fontes de abrangência nacional como DJEN, CONFAZ, BNP) |
| ente | texto? | UF ou `UF-Municipio`; obrigatório se esfera estadual/municipal |
| grupo | enum | `legislacao_federal`, `legislacao_estadual`, `legislacao_municipal`, `tributos`, `tribunais_judiciais`, `administrativos_contas` |
| natureza | enum | `oficial` ou `agregador_apoio` |
| responsavelInterno | lista enum | `AJ`, `AF`, `CC`, `TI` (o catálogo admite mais de um, ex.: `AJ / CC`) |
| formatos | lista enum | `html`, `pdf`, `xml`, `json`, `xsd` |
| tipoConector | enum | ver research R-03 |
| configuracaoConector | json | regras de extração versionadas |
| periodicidade | enum | `diaria_dias_uteis`, `diaria`, `semanal`, `quinzenal`, `trimestral` |
| ativa | bool | |

**Validação (FR-030)**: `natureza = oficial` exige domínio `.gov.br`, `.leg.br` ou `.jus.br` e
`orgaoEmissor`; caso contrário só é aceita como `agregador_apoio`. Fontes do catálogo cujo domínio
oficial não termine nesses sufixos exigem exceção registrada com aprovador.

### EnteMonitorado
| Campo | Tipo | Regras |
|---|---|---|
| id | uuid | |
| tipo | enum | `uf`, `municipio` |
| uf | char(2) | |
| municipio | texto? | obrigatório se tipo município |
| ativo | bool | só pode ser `true` com ao menos uma `Fonte` ativa do ente (FR-035) |

### FeriadoNacional
`data` (única), `descricao`, `fonteInformada`, `cadastradoPor`.

## Novidades

### Novidade *(operacional)*
| Campo | Tipo | Regras |
|---|---|---|
| id | uuid | |
| chaveDeduplicacao | texto | única (research R-05) |
| dataPublicacao | date | obrigatória |
| orgaoEmissor, tipoAto | texto | obrigatórios |
| numeroAto | texto? | |
| ementa | texto | obrigatória |
| linkOficial | texto? | obrigatório se `situacao = confirmada` |
| esfera | enum | `federal`, `estadual`, `municipal` (nunca `nacional`): herdada da fonte principal; se a fonte for `nacional`, definida por regra determinística a partir do órgão emissor do item (ex.: publicação de TJ no DJEN → estadual; convênio CONFAZ → estadual) |
| ente, grupo | | herdados da fonte principal ou da mesma regra |
| tema | texto? | sugerido pelo LLM (indício) |
| impacto | enum? | `alto`, `medio`, `baixo`; origem registrada |
| perfis | lista enum | `consultor_tributario`, `desenvolvedor`; ao menos um |
| relacionadaTributos | bool? | indício da IA; nulo se não classificada; só `false` oculta a novidade |
| origemPerfil, origemImpacto | enum | `regra_deterministica`, `indicio_ia` |
| situacao | enum | ver estados |
| retificaNovidadeId | FK? | vínculo com ato original |
| responsavelInterno | lista enum | da fonte principal |
| hashConteudo | texto | |
| buscaTexto | tsvector | gerado de ementa + tipo + número + órgão |

**Estados de `situacao`**:

```text
coletada ─┬─(fonte oficial do catálogo)────────────────► confirmada
          ├─(só agregador de apoio)─► pendente_confirmacao ─┐
          └─(fonte fora do catálogo)► fonte_nao_catalogada ─┤
                                                            ├─(revisão: confirma + link oficial)► confirmada
                                                            ├─(revisão: descarta + motivo)──────► descartada
                                                            └─(revisão: em análise)─────────────► em_analise ─► (confirma | descarta)
```

Regras: `confirmada` exige `linkOficial` de fonte `oficial`; `descartada` não aparece em consultas
nem boletins; transição só por revisão humana a partir de pendentes.

### OcorrenciaNovidade *(operacional)*
`novidadeId`, `fonteId?` (nulo se fora do catálogo), `urlEncontrada`, `coletadaEm`,
`execucaoColetaId`, `principal` (bool; exatamente uma principal por novidade).

### RevisaoNovidade
`novidadeId`, `revisorId`, `decisao` (`confirmar`, `descartar`, `em_analise`), `motivo`,
`linkOficialInformado`, `decididaEm`. Bloqueia o expurgo da novidade enquanto `em_analise`.

## Execuções

### ExecucaoColeta *(operacional)*
`id` (= execucaoId), `correlacaoId`, `fonteId`, `iniciadaEm`, `finalizadaEm`, `status`
(`sucesso`, `falha_tecnica`, `parcial`), `erro`, `novidadesEncontradas`.

### Consulta *(operacional)*
`id` (= execucaoId), `correlacaoId`, `usuarioId`, `conversaId?`, `assunto`, filtros (período,
esferas, perfis, grupos), `clienteId?`, `status`, `requerRevisaoHumana`, `limitacoes` (json),
`resultadoContratoVersao`.

### EtapaExecucao *(operacional)*
`execucaoId`, `etapa` (`coleta`, `classificacao`, `cruzamento_cliente`, `validacoes`,
`ferramentas`, `llm`, `validacao_saida`, `resposta`), `status`, `iniciadaEm`, `finalizadaEm`,
`detalhe`.

### RegistroConsumo *(operacional; prazo em DP-23)*
`execucaoId`, `correlacaoId`, `provedor`, `modelo`, `finalidade` (`classificacao`,
`relacao_nicho`, `resumo_boletim`, `conversa`), `tentativa`, `tokensEntrada?`, `tokensSaida?`,
`usoConhecido` (bool), `inicio`, `duracaoMs`, `status`, `custoEstimado?`, `custoInformado?`,
`moeda`, `tabelaPrecoId?`.

### TabelaPreco
`modelo`, `precoEntradaPorMilhao`, `precoSaidaPorMilhao`, `moeda`, `vigenteDesde`,
`urlReferencia`.

## Clientes

### Nicho
`codigo` (`varejo_mercadorias`, `produtos_farmaceuticos`, `tributacao_padrao`), `descricao`,
`versaoLista`, `ativo`. Lista fechada (Princípio VII).

### Cliente
| Campo | Tipo | Regras |
|---|---|---|
| id | uuid | |
| nome | texto | obrigatório, único (sem distinção de maiúsculas/acentos) |
| nichoCodigo | FK | deve existir e estar ativo |

Nenhum outro campo de cliente é permitido. Não sujeito à retenção de 30 dias.

## Boletim

### ListaDestinatarios
`id`, `nome` (inicialmente uma lista: `boletim-diario`).

### Destinatario
`listaId`, `nome`, `email` (único por lista, válido), `ativo`. Não sujeito à retenção.

### Boletim *(operacional)*
| Campo | Tipo | Regras |
|---|---|---|
| id | uuid | = execucaoId |
| listaId | FK | |
| diaReferenciaInicio, diaReferenciaFim | date | intervalo coberto (inclui fins de semana/feriados) |
| dataEnvioPrevista | date | dia útil |
| status | enum | `gerado`, `enviando`, `enviado`, `enviado_parcial`, `falha_tecnica` |
| resumoExecutivo | texto? | do LLM, validado; nulo se falhou |
| fontesNaoVerificadas | json | fontes diárias com falha no intervalo |
| semNovidades | bool | |

Restrição única: (`listaId`, `diaReferenciaFim`) — garante a FR-020.

### BoletimItem *(operacional)*
`boletimId`, `novidadeId`, `secao` (`confirmadas`, `confirmadas_apos_revisao`), `esfera`,
`perfil`. Restrição: uma novidade aparece no máximo uma vez em seção `confirmadas_apos_revisao`
em toda a história de boletins.

### EnvioBoletim *(operacional)*
`boletimId`, `destinatarioId`, `status` (`pendente`, `enviado`, `falha`), `tentativas`,
`ultimoErro`, `enviadoEm`. Única por (`boletimId`, `destinatarioId`).

## Memória conversacional (MongoDB)

### Conversa
`_id`, `usuarioId`, `conversaId`, `mensagens[]` (`papel`, `texto`, `em`), `atualizadoEm` (índice
TTL 30 dias). Índice único (`usuarioId`, `conversaId`). Não armazena dados de cliente além do
nome citado pelo próprio usuário na conversa.
