# Constitution — infis-agente-monitoramento-legislacao

Este documento é normativo. As palavras **DEVE**, **NÃO DEVE** e **PODE** indicam,
respectivamente, obrigação, proibição e opção. Toda especificação, plano, tarefa, implementação e
revisão de pull request (PR) DEVE ser compatível com esta constitution. Em caso de conflito, esta
constitution prevalece sobre qualquer outro documento do projeto, salvo desvio aprovado conforme a
Seção 8.

## 1. Propósito e escopo

### 1.1 Propósito

Construir um agente de IA que acompanhe publicações oficiais, leis, atos normativos e decisões
judiciais, e produza, de forma rastreável e verificável:

- alertas de mudanças normativas;
- análises de jurisprudência;
- análises de impacto tributário;
- resumos executivos.

### 1.2 Escopo

Estão no escopo:

- monitoramento de fontes oficiais previamente aprovadas, nas três esferas: federal, estadual e
  municipal, abrangendo legislação e atos normativos, tributos, tribunais judiciais, tribunais
  administrativos e tribunais de contas (Seção 4.4);
- acompanhamento simultâneo do modelo tributário atual (ICMS, ISS, PIS/Cofins) e do novo modelo
  da Reforma Tributária (IBS, CBS e Imposto Seletivo) durante a transição;
- classificação das novidades por esfera e por perfil de público (consultor tributário e
  desenvolvedor);
- busca sob demanda por assunto, com resposta no agente, e boletim diário por e-mail com as
  novidades do dia (Princípio XIX);
- cruzamento das novidades com o nicho de negócio de um cliente, quando solicitado;
- validação determinística de arquivos e registros;
- consulta a manuais e documentos oficiais por RAG;
- uso controlado de ferramentas externas;
- análise com LLM e resposta estruturada com evidências;
- exposição do agente para outros agentes e orquestradores por A2A (integração futura).

Estão fora do escopo, salvo nova especificação aprovada:

- emissão de parecer jurídico ou tributário com valor de decisão final sem revisão humana;
- acesso a dados do cliente além de nome e nicho de negócio (Princípio VII);
- criação de regras fiscais, layouts, schemas ou critérios de conformidade sem fonte oficial.

### 1.3 Fonte de verdade normativa

Layouts, regras fiscais, schemas e critérios de conformidade NÃO DEVEM ser inventados pela equipe
nem pelo LLM. Eles DEVEM ser obtidos de documentos oficiais e registrados na especificação da
funcionalidade com origem, identificação e versão.

## 2. Princípios obrigatórios

Cada princípio contém **Regra**, **Justificativa** e **Verificação**. A verificação descreve o que
o revisor de PR ou o pipeline DEVE checar.

### I. Especificação antes da implementação

**Regra.** Nenhuma funcionalidade DEVE ser implementada sem especificação aprovada, plano técnico e
tarefas derivados dela. Todo requisito DEVE ter critério de aceite verificável e DEVE ser
rastreável até tarefas e testes.

**Justificativa.** O domínio é regulatório; comportamento não especificado vira risco de
interpretação incorreta e de alerta indevido.

**Verificação.** O PR DEVE referenciar a especificação e as tarefas atendidas. Cada requisito
alterado DEVE ter ao menos um teste associado por identificador (ex.: `FR-012` → `T034` → teste).

### II. Fontes oficiais e proibição de invenção normativa

**Regra.** O agente DEVE buscar novidades prioritariamente nas fontes do catálogo de fontes
confiáveis (Seção 4.4). O catálogo DEVE ser persistido no PostgreSQL da Api.Agente, versionado.
Uma fonte só é considerada confiável quando atende a todos os critérios:

- é mantida pelo próprio órgão emissor;
- usa domínio oficial (`.gov.br`, `.leg.br` ou `.jus.br`);
- publica texto com validade jurídica (diário oficial ou DJEN) ou base consolidada oficial;
- permite verificar data e número do ato.

Agregadores (ex.: Querido Diário) e bases privadas PODEM ser usados apenas como apoio de busca.
Uma novidade encontrada por agregador NÃO DEVE ser tratada como confirmada antes de o texto ser
localizado na publicação oficial de origem; até lá, DEVE ser marcada como pendente de
confirmação. Novidade encontrada em base fora do catálogo DEVE ser marcada como "fonte não
catalogada" e DEVE exigir revisão humana (Princípio XVI); ela NÃO DEVE gerar alerta conclusivo
nem entrar no boletim como novidade confirmada antes dessa revisão. Uma fonte só entra no
catálogo por alteração aprovada, nunca por decisão do agente. Layouts, regras fiscais, schemas e critérios de
conformidade NÃO DEVEM ser criados, completados ou presumidos sem documento oficial registrado.

**Justificativa.** Alertas baseados em fontes não oficiais ou em regras presumidas comprometem a
confiabilidade do produto e podem gerar decisões tributárias erradas.

**Verificação.** Toda regra codificada DEVE citar, em especificação ou ADR, o documento oficial,
a versão e a localização (artigo, item, seção ou página). Inclusão de fonte nova DEVE ocorrer por
alteração versionada do catálogo, nunca por configuração ad hoc em código. Testes DEVEM provar que
um achado vindo apenas de agregador ou de fonte não catalogada não é apresentado como confirmado.

### III. Determinismo antes do LLM

**Regra.** Regras estruturais e regras de negócio determinísticas DEVEM ser implementadas como
validadores ou ferramentas verificáveis e testáveis. O LLM NÃO DEVE substituir, contornar ou
reinterpretar o resultado dessas validações. O LLM PODE explicar, resumir e correlacionar
resultados já produzidos pelos validadores.

**Justificativa.** Validações determinísticas são reproduzíveis e auditáveis; a saída do LLM não é.

**Verificação.** Cada regra determinística DEVE existir como componente com testes unitários
cobrindo casos válidos, inválidos e limites. Revisor DEVE rejeitar PR em que uma regra
determinística esteja apenas descrita em prompt.

### IV. Classificação obrigatória dos achados e proibição de falsa conformidade

**Regra.** Todo achado DEVE ser classificado em exatamente uma das categorias:

1. **Erro estrutural do documento** — violação de layout, schema ou formato oficial.
2. **Inconsistência de negócio** — violação de regra de negócio determinística.
3. **Indício identificado pela IA** — hipótese do LLM, não confirmada por validador.
4. **Ausência de evidências** — não há base documental suficiente para concluir.
5. **Falha técnica ou dependência indisponível** — erro de execução, timeout ou serviço fora.

O agente NÃO DEVE declarar conformidade, ausência de impacto ou resultado conclusivo quando
qualquer validação obrigatória não tiver sido concluída com sucesso. Indícios da IA NÃO DEVEM ser
apresentados como erro estrutural ou inconsistência de negócio.

**Justificativa.** Misturar hipótese, falha técnica e violação confirmada induz o usuário a
conclusões erradas.

**Verificação.** O contrato de saída DEVE conter o campo de categoria com essas cinco opções
(nomes técnicos definidos na especificação). Testes DEVEM provar que falha de dependência resulta
em status não conclusivo, e nunca em conformidade.

### V. Evidência e rastreabilidade

**Regra.** Toda conclusão, alerta ou impacto DEVE apontar evidências recuperáveis: fonte,
identificador do documento, versão, data de publicação e localização, quando disponível. Toda
execução DEVE possuir identificador único e identificador de correlação propagados entre
Api.Agente, Api.RAG, ferramentas e chamadas ao LLM. Evidências, resultados de ferramentas e
referências NÃO DEVEM ser fabricados.

**Justificativa.** Sem rastreabilidade não há auditoria, revisão humana nem defesa da análise.

**Verificação.** Testes DEVEM validar que toda referência retornada existe na base de conhecimento
ou no resultado da ferramenta da mesma execução. Logs DEVEM permitir reconstruir a execução a
partir do identificador de correlação.

### VI. Fluxo obrigatório de busca e análise

**Regra.** Toda execução de monitoramento ou análise DEVE seguir, nesta ordem:

1. Buscar novidades de legislação e jurisprudência nas fontes confiáveis aprovadas, marcando as
   encontradas fora do catálogo (Princípio II).
2. Classificar as novidades por esfera (federal, estadual, municipal, com o ente federativo
   quando houver) e por perfil de público: consultor tributário e desenvolvedor.
3. Quando a consulta envolver um cliente, cruzar as novidades com o nicho de negócio do cliente.
4. Executar validações determinísticas aplicáveis.
5. Executar ferramentas externas somente quando necessárias e autorizadas (Princípio XII).
6. Produzir a análise com o LLM.
7. Validar a saída estruturada (Princípio XI).
8. Retornar o resultado com evidências e rastreabilidade (Princípio V).

Etapas não executadas DEVEM ser registradas como limitações ou validações não realizadas.

**Justificativa.** Um fluxo fixo garante que o LLM opere sobre dados já filtrados e validados.

**Verificação.** O orquestrador DEVE registrar o início, fim e resultado de cada etapa. Testes de
integração DEVEM cobrir o fluxo completo e os desvios por falha em cada etapa.

### VII. Minimização de dados do cliente

**Regra.** O agente DEVE ter acesso somente ao **nome do cliente** e ao seu **nicho de negócio**.
Os nichos aceitos DEVEM pertencer a uma lista fechada e versionada; a lista inicial é:

- "Venda de mercadorias de varejo";
- "Venda de produtos farmacêuticos";
- "Tributação padrão" — nicho para os clientes com tributação padrão, que são a maioria dos
  atendidos (nome técnico definitivo em DP-05).

Qualquer outro dado do cliente NÃO DEVE ser recebido, persistido, enviado ao LLM ou a
ferramentas externas. Nichos fora da lista DEVEM ser rejeitados com erro de validação. O cadastro
de clientes DEVE ficar no PostgreSQL da Api.Agente, separado da base do RAG, e NÃO DEVE ser
ingerido como conhecimento nem convertido em embeddings.

**Justificativa.** Minimização reduz exposição a dados sensíveis (LGPD) e limita o raciocínio do
agente ao que é necessário para o cruzamento.

**Verificação.** O contrato de entrada DEVE aceitar apenas esses campos. Testes DEVEM provar que
campos adicionais são rejeitados ou descartados antes de qualquer persistência ou chamada externa.

**Solicitações da SOFIA (v2.1.0).** Este princípio rege o cadastro de clientes usado no cruzamento
por nicho. O texto de documentos recebido do orquestrador SOFIA pelo contrato A2A (ADR 0009) PODE ser
enviado ao LLM somente como contexto da pergunta, delimitado como dado não confiável (Princípio X), e
NÃO DEVE ser persistido, registrado em log, ingerido no RAG nem associado ao cadastro de clientes. O
nome do usuário enviado pela SOFIA NÃO DEVE ser enviado ao LLM, persistido nem registrado em log.
Testes DEVEM provar essas restrições.

### VIII. Segmentação por perfil de público

**Regra.** Cada novidade DEVE ser classificada para um ou ambos os perfis, segundo os critérios:

- **Desenvolvedor**: tudo que envolva layout, adequação de sistemas ou atualização de API
  (incluindo schemas, regras de validação de documentos e notas técnicas).
- **Consultor tributário**: especificações da legislação (obrigações, alíquotas, prazos,
  interpretação e jurisprudência) e notícias sobre legislação.

Uma novidade que atenda aos dois critérios DEVE ser atribuída aos dois perfis. A especificação
DEVE detalhar exemplos de cada critério. A classificação DEVE registrar sua origem: regra
determinística ou indício da IA.

**Justificativa.** Os públicos têm necessidades e ações distintas; alertas genéricos reduzem
utilidade e aumentam ruído.

**Verificação.** O contrato de saída DEVE conter o perfil e a origem da classificação. Casos de
teste representativos DEVEM existir para cada perfil.

### IX. Conhecimento versionado e recuperável (RAG)

**Regra.**

- Todo manual ou documento ingerido DEVE possuir origem, identificação e versão.
- Todo chunk DEVE permitir recuperar documento de origem, versão e localização, quando disponível.
- A ingestão DEVE ter critérios documentados de segmentação, deduplicação, reprocessamento e
  atualização de versões.
- Modelo de embeddings, dimensão dos vetores e estratégia de busca DEVEM ser compatíveis entre si
  e registrados em ADR. Troca de modelo de embeddings DEVE exigir plano de reprocessamento.
- A recuperação DEVE filtrar por tipo de documento e pela versão do manual aplicável.
- Tamanho de chunk, sobreposição, topK e limiares de similaridade NÃO DEVEM ser fixados sem
  especificação e avaliação com exemplos representativos.
- Respostas fundamentadas no RAG DEVEM apresentar referências recuperáveis.
- Na ausência de evidências suficientes, o agente DEVE declarar a limitação (categoria 4 do
  Princípio IV), e NÃO DEVE completar com conhecimento não referenciado.

**Justificativa.** Versões de manuais mudam com frequência; uma resposta baseada em versão errada é
uma resposta errada.

**Verificação.** O plano técnico DEVE conter o conjunto de avaliação de recuperação e seus
resultados. Testes DEVEM provar que filtros por tipo e versão são aplicados.

### X. Conteúdo externo é dado não confiável

**Regra.** Conteúdo de arquivos, páginas, publicações, chunks e respostas de ferramentas DEVE ser
tratado como dado não confiável. Esse conteúdo NÃO DEVE alterar instruções do sistema, regras,
autorizações, ferramentas disponíveis ou formato de saída. Conteúdo externo DEVE ser delimitado e
identificado como dado ao ser enviado ao LLM.

**Justificativa.** Publicações e documentos podem conter texto que funciona como injeção de prompt.

**Verificação.** A suíte de testes DEVE incluir casos de injeção de prompt em documentos e chunks,
comprovando que não há mudança de comportamento, chamada de ferramenta não autorizada ou vazamento.

### XI. Saída estruturada, versionada e validada

**Regra.**

- Modelo e parâmetros de execução do LLM DEVEM ser configurados externamente ao código.
- A resposta do LLM DEVE seguir contrato JSON explícito e versionado, usando o mecanismo de saída
  estruturada compatível com o modelo escolhido.
- A resposta DEVE passar por validação estrutural (schema) e semântica (regras do domínio, existência
  das referências, coerência entre status e achados) antes de ser aceita.
- Respostas inválidas DEVEM seguir política de retentativa limitada e documentada; esgotadas as
  tentativas, o resultado DEVE ser retornado como falha técnica, nunca como análise válida.
- O contrato do LLM DEVE contemplar, conforme a especificação: resultado da análise; achados com
  categoria e severidade; evidências e referências; limitações e validações não realizadas;
  necessidade de revisão humana.
- O **envelope da API** DEVE ser distinto do JSON produzido pelo LLM. Identificador da execução,
  status do processamento, consumo, custos, tempos, versão do contrato e demais metadados
  operacionais DEVEM ser preenchidos pelo sistema, e NÃO DEVEM ser solicitados ou aceitos do modelo.

**Justificativa.** Contratos explícitos permitem integração com outros agentes e impedem que o
modelo invente metadados.

**Verificação.** Schemas versionados DEVEM existir no repositório. Testes de contrato DEVEM cobrir
respostas válidas, inválidas, parciais e com referências inexistentes. Mudança incompatível no
contrato DEVE incrementar a versão principal do contrato.

### XII. Ferramentas governadas

**Regra.** Cada ferramenta exposta ao LLM (Semantic Kernel / Function Calling ou MCP) DEVE possuir:
nome e responsabilidade claros; contratos de entrada e saída; validação de argumentos; regras de
autenticação e autorização; timeout e tratamento de falhas; política de retentativa e
idempotência, quando aplicável; testes; e registro de execução. A seleção de uma ferramenta pelo
LLM NÃO DEVE conceder autorização para executá-la: a autorização DEVE ser decidida pelo sistema
antes da execução, com base no contexto do usuário e da execução.

**Justificativa.** O LLM decide o que pedir, não o que é permitido.

**Verificação.** Cada ferramenta DEVE ter ficha técnica (no plano ou em documentação) com os itens
acima. Testes DEVEM provar a recusa de chamadas não autorizadas e de argumentos inválidos.

### XIII. Propriedade de dados e comunicação por MCP

**Regra.** Cada serviço DEVE ser proprietário dos seus dados. A comunicação funcional entre
Api.Agente e Api.RAG DEVE ocorrer por MCP. A Api.Agente NÃO DEVE acessar diretamente tabelas,
vetores, esquemas ou cadeias de conexão da Api.RAG.

**Justificativa.** Acoplamento por banco impede evolução independente e quebra o isolamento de
responsabilidades.

**Verificação.** Revisor DEVE rejeitar PR em que a Api.Agente referencie conexão, entidade ou
repositório da Api.RAG. Testes de integração DEVEM exercitar a recuperação via MCP.

### XIV. Memória conversacional isolada

**Regra.** A memória conversacional DEVE usar MongoDB (decisão DP-10, a ser registrada em ADR com
a configuração adotada). DEVE haver isolamento por usuário e conversa, e por organização quando
aplicável; expiração automática conforme a Seção 4.3 (30 dias); exclusão sob solicitação; limite do histórico enviado ao modelo;
proteção de dados sensíveis; e separação estrita entre histórico conversacional e conhecimento
oficial. O histórico NÃO DEVE ser usado como fonte de evidência normativa.

**Justificativa.** Vazamento entre conversas e confusão entre memória e fonte oficial são riscos
diretos de privacidade e de qualidade.

**Verificação.** Testes DEVEM provar isolamento entre usuários/conversas, expiração e exclusão.
Referências de evidência NÃO DEVEM apontar para o histórico.

### XV. Consumo e custos fiéis

**Regra.** Todo consumo DEVE ser persistido em PostgreSQL por chamada, incluindo retentativas e
chamadas adicionais, com: execução e correlação; provedor e modelo; tokens de entrada e saída;
consumo de embeddings, quando aplicável; data, duração e status; estimativa de custo e referência
da tabela de preços utilizada. Valores estimados DEVEM ser distinguidos de valores informados pelo
provedor. Consumo desconhecido DEVE ser registrado como desconhecido (nulo com indicador), e NÃO
DEVE ser registrado como zero.

**Justificativa.** Custos de LLM e embeddings são variáveis e precisam ser auditáveis.

**Verificação.** Testes DEVEM cobrir chamada bem-sucedida, retentativa, falha e ausência de dados de
uso do provedor. O registro DEVE referenciar a versão da tabela de preços.

### XVI. Revisão humana explícita

**Regra.** A resposta DEVE indicar se requer revisão humana. A revisão humana DEVE ser indicada,
no mínimo, quando:

- a novidade ou evidência vier de fonte fora do catálogo de fontes confiáveis (critério principal);
- houver indício da IA sem confirmação determinística em ponto relevante;
- faltarem evidências em ponto relevante;
- uma validação obrigatória não tiver sido concluída.

A especificação PODE acrescentar critérios, mas NÃO DEVE remover estes.

**Justificativa.** O agente apoia a decisão tributária; não a substitui. Fontes não catalogadas
têm confiabilidade desconhecida.

**Verificação.** Testes DEVEM provar que esses cenários resultam em indicação de revisão humana,
em especial o de fonte fora do catálogo.

### XVII. Simplicidade e separação de camadas

**Regra.** Cada serviço DEVE separar endpoints, aplicação, domínio e infraestrutura. O domínio NÃO
DEVE depender de infraestrutura, SDKs de LLM, Semantic Kernel, MCP ou frameworks web. Projetos,
bibliotecas, padrões ou dependências adicionais DEVEM ser justificados no plano técnico ou em ADR,
com o problema que resolvem e a alternativa mais simples descartada.

**Justificativa.** Complexidade sem necessidade aumenta custo de manutenção e dificulta revisão.

**Verificação.** Revisor DEVE checar a direção das dependências entre projetos e a justificativa de
qualquer novo projeto ou pacote. O plano DEVE preencher a tabela de complexidade quando houver
desvio.

### XVIII. Observabilidade e falhas explícitas

**Regra.** Toda execução DEVE produzir logs estruturados, com identificador de execução e de
correlação, por etapa do fluxo, por ferramenta e por chamada ao LLM e ao RAG. Falhas DEVEM ser
explícitas e classificadas (categoria 5 do Princípio IV); NÃO DEVE haver captura silenciosa de
exceções. Logs NÃO DEVEM conter segredos nem dados sensíveis.

**Justificativa.** Diagnóstico e auditoria dependem de registros consistentes.

**Verificação.** Testes ou revisão DEVEM confirmar a presença dos identificadores nos logs e a
ausência de segredos e dados sensíveis.

### XIX. Entrega de alertas: consulta no agente e boletim diário

**Regra.** O agente DEVE oferecer dois modos de entrega:

1. **Sob demanda**: o usuário envia um assunto ao agente e recebe as novidades encontradas na
   própria resposta do agente.
2. **Boletim diário por e-mail**: envio, para uma lista de funcionários cadastrada, de um boletim
   contendo **somente as novidades publicadas no dia de referência** do boletim.

O boletim DEVE agrupar as novidades por esfera (federal, estadual, municipal) e por perfil
(consultor tributário, desenvolvedor), com evidências e referências conforme o Princípio V.
Novidades de fonte não catalogada e ainda não revisadas NÃO DEVEM aparecer no boletim como
confirmadas; PODEM aparecer em seção separada, identificadas como pendentes de revisão. O envio
DEVE ser idempotente por dia de referência e lista, para evitar boletins duplicados. A lista de
destinatários DEVE ser gerida pelo sistema, e NÃO DEVE ser alterada pelo LLM nem por conteúdo
externo. Dia vazio DEVE ser informado como "sem novidades", e não omitido silenciosamente, salvo
decisão contrária na especificação.

**Justificativa.** O boletim diário é o principal canal de consumo da equipe; duplicidade, envio
de novidades antigas ou de fonte não confirmada reduz confiança.

**Verificação.** Testes DEVEM cobrir: filtro pela data de publicação do dia, agrupamento por
esfera e perfil, idempotência do envio, exclusão de itens não revisados da seção confirmada e
falha do provedor de e-mail registrada como falha técnica.

## 3. Restrições arquiteturais

### 3.1 Plataforma

- A solução DEVE usar .NET 10 com ASP.NET Core.
- A integração com LLM DEVE usar a OpenAI. Troca de provedor exige alteração desta constitution.
- A orquestração de ferramentas por Function Calling DEVE usar Semantic Kernel.
- Vetores DEVEM ser armazenados em PostgreSQL com PgVector.
- Consumo e custos DEVEM ser persistidos em PostgreSQL.
- Versões de SDKs, pacotes e modelos DEVEM ser definidas nos planos técnicos, não nesta constitution.

### 3.2 Serviços principais

**Api.Agente** — responsável por:

- disponibilizar API REST para execução do agente;
- receber arquivos e solicitações de análise;
- orquestrar o fluxo do Princípio VI e as validações determinísticas;
- integrar-se à OpenAI;
- consultar a Api.RAG como **cliente MCP**;
- executar ferramentas por Semantic Kernel (Function Calling);
- gerenciar memória conversacional;
- registrar consumo de tokens e custos;
- validar a resposta estruturada do LLM;
- expor o agente por **servidor A2A**.

**Api.RAG** — responsável por:

- disponibilizar API REST para administração da base de conhecimento;
- disponibilizar endpoint POST de ingestão;
- processar documentos, gerar chunks e embeddings;
- armazenar e consultar vetores em PostgreSQL com PgVector;
- expor ferramentas de recuperação de conhecimento por **servidor MCP**.

### 3.3 Regras de fronteira

- Api.Agente e Api.RAG DEVEM ser implantáveis e testáveis de forma independente.
- A comunicação funcional entre ambas DEVE ocorrer por MCP. A API REST da Api.RAG é destinada à
  administração da base e NÃO DEVE ser usada pela Api.Agente para recuperação.
- Bancos e esquemas de cada serviço NÃO DEVEM ser compartilhados.
- A exposição A2A DEVE reutilizar os mesmos contratos versionados da API REST; NÃO DEVE criar
  caminho de execução que ignore validações, autorização ou registro de consumo.

### 3.4 Organização interna

Cada serviço DEVE manter, no mínimo, as responsabilidades: endpoints (REST, MCP, A2A), aplicação
(casos de uso e orquestração), domínio (entidades, regras e validadores determinísticos) e
infraestrutura (persistência, clientes externos, LLM, MCP). A forma física (projetos ou pastas)
DEVE ser decidida no plano técnico, seguindo o Princípio XVII.

## 4. Contratos e governança dos dados

### 4.1 Contratos

- Contratos de entrada, saída do LLM, envelope da API, ferramentas, MCP e A2A DEVEM ser explícitos,
  versionados e mantidos no repositório.
- Alterações incompatíveis DEVEM incrementar a versão principal do contrato e registrar plano de
  migração para consumidores.
- O envelope da API DEVE conter, no mínimo, os campos preenchidos pelo sistema: identificador da
  execução, identificador de correlação, status do processamento, versão do contrato, carimbos de
  tempo, consumo e custo (com indicação de estimado/informado/desconhecido) e o conteúdo validado
  produzido pelo LLM.
- Escala de severidade e nomes técnicos das categorias DEVEM ser definidos na especificação (DP-07).

### 4.2 Propriedade e classificação

| Dado | Proprietário | Armazenamento |
|---|---|---|
| Documentos, chunks, embeddings, metadados de versão | Api.RAG | PostgreSQL + PgVector |
| Execuções, achados, boletins, consumo e custos | Api.Agente | PostgreSQL |
| Memória conversacional | Api.Agente | MongoDB |
| Catálogo de fontes confiáveis | Api.Agente | PostgreSQL |
| Cadastro de clientes (somente nome e nicho) | Api.Agente | PostgreSQL (fora do RAG) |
| Lista de destinatários do boletim | Api.Agente | PostgreSQL |

### 4.3 Regras de dados

- Documentos oficiais DEVEM registrar origem, identificação, versão, data de publicação e data de
  ingestão.
- Novas versões de documentos NÃO DEVEM sobrescrever versões anteriores sem preservar a
  rastreabilidade de análises já emitidas.
- Retenção: dados operacionais (memória conversacional, execuções, achados, boletins gerados,
  logs e registros de consumo e custo) DEVEM ser excluídos automaticamente após **30 dias**.
  O cadastro de clientes e o catálogo de fontes confiáveis NÃO estão sujeitos a esse prazo e
  DEVEM ser mantidos até exclusão explícita. A retenção da base de conhecimento do RAG segue o
  versionamento de documentos (DP-21).
- A exclusão por retenção NÃO DEVE remover dados necessários a uma revisão humana ainda aberta;
  a especificação DEVE definir esse tratamento.

### 4.4 Catálogo de fontes confiáveis

**Documento de origem.** A carga inicial do catálogo é o documento "Catálogo de Fontes
Confiáveis – Monitoramento Legislativo e Jurisprudencial", de 2026-10-29, de autoria de Lettícia
Dev, versionado em `docs/fontes/catalogo-fontes-confiaveis-2026-10-29.pdf`. Uma nova versão do
catálogo DEVE ser adicionada como novo arquivo datado nessa pasta, sem sobrescrever a anterior, e
DEVE gerar nova versão do catálogo persistido.

**Abrangência.** O catálogo cobre seis grupos, e a especificação de monitoramento DEVE tratar
cada um:

1. Esfera federal — legislação e atos normativos (ex.: DOU, Planalto, Senado, Câmara, LexML,
   ministérios e agências reguladoras).
2. Esfera estadual — DOE, Assembleia Legislativa, Executivo estadual e PGE, por UF monitorada.
3. Esfera municipal — DOM, Câmara Municipal e PGM, por município monitorado.
4. Tributos — RFB, PGFN, CGSN, CGIBS, Ministério da Fazenda (CBS), CONFAZ, SEFAZ, fazendas
   municipais, portais de NF-e/SPED/NFS-e Nacional e eSocial.
5. Tribunais judiciais — DJEN, BNP, STF, STJ, TST, TSE, CJF/TRFs, TJs, TRTs e DataJud.
6. Tribunais administrativos e de contas — CARF, conselhos estaduais e municipais, instâncias do
   CGIBS, TCU, TCEs e TCMs.

**Campos obrigatórios por fonte.** Identificador, nome, link oficial, o que monitorar, órgão
emissor, esfera, grupo (1 a 6), responsável interno, formato (HTML, PDF, XML, JSON, XSD, API),
local de armazenamento e periodicidade de coleta.

**Precedência entre fontes.** Quando houver mais de uma fonte para o mesmo ato, a publicação com
validade jurídica (DOU, DOE, DOM, DJEN) DEVE prevalecer como evidência. Bases compiladas e APIs
PODEM ser usadas para texto consolidado, histórico e coleta automatizada. Para jurisprudência,
precedentes qualificados (repercussão geral, repetitivos, súmulas, IRDR e IAC) DEVEM ser
priorizados.

**Periodicidade.** A coleta DEVE respeitar a periodicidade do catálogo para cada tipo de fonte.
Uma fonte coletada com periodicidade maior que diária NÃO DEVE ter suas novidades apresentadas
como "do dia" no boletim sem que a data de publicação seja informada (ver DP-22).

**Metadados do documento coletado.** Todo ato coletado DEVE registrar: data de publicação, órgão
emissor, tipo e número do ato, ementa, link da fonte oficial, tema, impacto, áreas ou clientes
afetados e responsável pela análise. A nomenclatura de arquivo DEVE seguir o padrão do catálogo
(`AAAA-MM-DD_ESFERA_ORGAO_TIPO-NUMERO.ext`). A escala de impacto do catálogo (alto, médio,
baixo) DEVE ser usada para novidades; ela não substitui a escala de severidade de achados
(DP-07).

**Papéis internos.** O catálogo define os papéis AJ (Analista Jurídico), AF (Analista
Fiscal/Tributário), CC (Coordenador de Compliance) e TI (Analista de Dados/TI). O agente DEVE
registrar o responsável interno da fonte em cada novidade. O agente NÃO DEVE executar a validação
atribuída a um papel humano; o vínculo entre esses papéis e a revisão humana fica em DP-26.

**Revisão do catálogo.** Links e fontes DEVEM ser revisados periodicamente, conforme a
periodicidade definida no catálogo. Link indisponível DEVE ser tratado como falha de dependência
(categoria 5 do Princípio IV), e não como ausência de novidades.
- Dados pessoais DEVEM seguir a LGPD; somente o mínimo necessário (Princípio VII) PODE ser tratado.

## 5. Segurança e qualidade

### 5.1 Segurança

- Segredos (chaves da OpenAI, cadeias de conexão, tokens) NÃO DEVEM estar no código-fonte nem em
  logs. PODEM ficar nos arquivos `appsettings*.json` da aplicação, inclusive para o deploy, ou em
  provedor de segredos.
- Endpoints REST, MCP e A2A DEVEM exigir autenticação e autorização (mecanismo em DP-13).
- Arquivos recebidos DEVEM ter tipo, tamanho e conteúdo validados antes do processamento.
- Chamadas a fontes e ferramentas externas DEVEM ocorrer somente para destinos permitidos.
- Conteúdo externo DEVE seguir o Princípio X.
- Dependências DEVEM ser verificadas quanto a vulnerabilidades conhecidas no pipeline.

### 5.2 Qualidade e testes

- Validadores determinísticos DEVEM ter testes unitários.
- Contratos (LLM, envelope, ferramentas, MCP, A2A) DEVEM ter testes de contrato.
- Fluxo do Princípio VI e comunicação Api.Agente ↔ Api.RAG DEVEM ter testes de integração.
- Testes automatizados NÃO DEVEM depender de chamadas reais ao LLM para passar; respostas do LLM
  DEVEM ser simuladas em testes determinísticos. Avaliações com o LLM real PODEM existir em suíte
  separada.
- Qualidade da recuperação e da análise DEVE ser medida com conjunto de avaliação representativo,
  com métricas e limites definidos no plano (DP-15).
- Casos de injeção de prompt e de dependência indisponível DEVEM estar cobertos.

## 6. Fluxo de desenvolvimento SDD

O fluxo obrigatório é:

**constitution → especificação → esclarecimentos → plano técnico → tarefas → implementação →
validação**

- **Especificação** (`/speckit-specify`): requisitos funcionais, critérios de aceite verificáveis,
  fontes oficiais aplicáveis e marcação explícita do que é desconhecido.
- **Esclarecimentos** (`/speckit-clarify`): toda ambiguidade relevante DEVE ser resolvida ou
  registrada como decisão pendente; suposições NÃO DEVEM ser promovidas a requisito aprovado.
- **Plano técnico** (`/speckit-plan`): DEVE conter a verificação de conformidade com esta
  constitution (princípios I–XIX), parâmetros ajustáveis, contratos e ADRs necessários.
- **Tarefas** (`/speckit-tasks`): cada tarefa DEVE referenciar os requisitos atendidos e os testes.
- **Implementação** (`/speckit-implement`): somente de tarefas aprovadas.
- **Validação** (`/speckit-analyze` e revisão de PR): consistência entre especificação, plano,
  tarefas, código e testes.

Decisões arquiteturais relevantes DEVEM ser registradas em ADRs com contexto, decisão, alternativas
consideradas e consequências. São relevantes, no mínimo: escolha de armazenamento de memória,
modelo de embeddings e estratégia de busca, modelo do LLM, mecanismo de autenticação, novos
projetos ou bibliotecas e qualquer desvio desta constitution.

## 7. Definition of Done

Uma tarefa ou funcionalidade só está concluída quando TODOS os itens abaixo forem verdadeiros:

- [ ] Requisitos e critérios de aceite atendidos e rastreados até tarefas e testes.
- [ ] Regras derivadas de documento oficial citam origem, versão e localização.
- [ ] Validações determinísticas implementadas fora do LLM e testadas.
- [ ] Achados classificados nas cinco categorias do Princípio IV.
- [ ] Nenhum caminho declara conformidade com validação obrigatória pendente ou falha.
- [ ] Contratos versionados e testes de contrato aprovados.
- [ ] Envelope da API com metadados preenchidos pelo sistema.
- [ ] Ferramentas novas com ficha técnica completa (Princípio XII) e testes de autorização.
- [ ] Nenhum acesso da Api.Agente aos dados internos da Api.RAG.
- [ ] Somente nome e nicho do cliente tratados.
- [ ] Novidades classificadas por esfera e perfil; fontes não catalogadas marcadas para revisão.
- [ ] Retenção de 30 dias aplicada aos dados operacionais afetados.
- [ ] Consumo e custo registrados, incluindo retentativas, sem zeros para valores desconhecidos.
- [ ] Logs estruturados com correlação, sem segredos ou dados sensíveis.
- [ ] Testes de injeção de prompt e de dependência indisponível, quando aplicável.
- [ ] Testes automatizados passando no pipeline sem dependência de LLM real.
- [ ] ADRs criados para decisões relevantes; desvios registrados conforme a Seção 8.
- [ ] Documentação de configuração atualizada (parâmetros externos, sem valores secretos).

## 8. Governança e alterações

### 8.1 Supremacia e conformidade

- Esta constitution prevalece sobre especificações, planos, tarefas e convenções do projeto.
- Todo PR DEVE ser revisado quanto à conformidade com os princípios afetados; o revisor DEVE
  indicar o princípio violado ao rejeitar.

### 8.2 Desvios

Um desvio PODE ser aceito somente se registrado em ADR com: princípio afetado, justificativa,
impacto, responsável, aprovador, data de aprovação e, se temporário, prazo de revisão. Desvios
temporários vencidos DEVEM bloquear novos PRs da área afetada até serem resolvidos ou renovados.
NÃO DEVEM ser objeto de desvio: Princípios IV (falsa conformidade), V (fabricação de evidências),
VII (minimização de dados) e X (conteúdo não confiável).

### 8.3 Alterações desta constitution

- Toda alteração DEVE registrar versão, data e avaliação de impacto nas especificações, planos e
  ADRs existentes.
- Versionamento semântico:
  - **MAJOR** — remoção ou redefinição incompatível de princípio ou restrição.
  - **MINOR** — novo princípio, nova seção ou ampliação material de regra.
  - **PATCH** — esclarecimentos e correções sem mudança de significado.
- Especificações afetadas por alteração MAJOR ou MINOR DEVEM ser revisadas antes de nova
  implementação na área.

### 8.4 Revisão periódica

A constitution DEVE ser revisada ao menos quando houver mudança de provedor de LLM, de modelo de
embeddings, de arquitetura de serviços ou de escopo regulatório.

## 9. Decisões pendentes

Nenhum item pendente é requisito aprovado. Cada decisão pendente DEVE ser resolvida na etapa
indicada e registrada na especificação ou em ADR.

### 9.1 Decisões resolvidas

| ID | Decisão | Onde foi incorporada |
|---|---|---|
| DP-01 | Nome oficial da solução: **infis-agente-monitoramento-legislacao**. | Título |
| DP-02 | Monitoramento nas três esferas (federal, estadual, municipal), com reporte separado por esfera. | 1.2, VI, XIX |
| DP-04 | Busca sob demanda por assunto no agente e boletim diário por e-mail para lista de funcionários, só com as novidades do dia. | 1.2, XIX |
| DP-06 | Desenvolvedor: layout, adequação de sistemas, atualização de API. Consultor: especificações da legislação e notícias. | VIII |
| DP-08 | Revisão humana obrigatória para buscas e novidades fora do catálogo de fontes confiáveis, além dos critérios mínimos. | II, XVI |
| DP-10 | Memória conversacional em MongoDB. | XIV, 4.2 |
| DP-11 | Retenção de 30 dias para dados operacionais; cadastro de clientes e catálogo de fontes excluídos do prazo. | 4.3 |
| DP-12 | Cadastro de clientes no PostgreSQL da Api.Agente, fora do RAG. | VII, 4.2 |
| DP-03 | Catálogo inicial: documento "Catálogo de Fontes Confiáveis – Monitoramento Legislativo e Jurisprudencial" (2026-10-29), com critérios de confiabilidade, grupos, periodicidade e metadados. (v1.2.0) | II, 4.4 |
| DP-18 | A2A v0.3.x, JSON-RPC 2.0, `message/send` síncrono, sem streaming nem push; contrato SOFIA v1.0 em TextPart e resposta no Artifact `analise-sofia-v1`; Agent Card público. (v2.1.0) | 3.3, VII, ADR 0009 |

### 9.2 Decisões pendentes

| ID | Decisão pendente | Etapa de resolução |
|---|---|---|
| DP-05 | Nichos: "Venda de medicamentos" foi removido e "Tributação padrão" foi criado. Falta confirmar o nome definitivo e o critério para enquadrar o cliente nesse nicho. | Esclarecimentos |
| DP-07 | Escala de severidade, nomes técnicos das categorias e status de processamento. (Adiado pelo responsável.) | Especificação |
| DP-09 | Tipos de arquivos e registros sujeitos à validação determinística e respectivos documentos oficiais. | Especificação por funcionalidade |
| DP-13 | Mecanismo de autenticação e autorização (REST, MCP, A2A) e modelo de organização/tenant. | Plano técnico + ADR |
| DP-14 | Modelo da OpenAI, modelo de embeddings, dimensão e estratégia de busca (vetorial, híbrida). | Plano técnico + ADR |
| DP-15 | Parâmetros de chunking, topK, limiares e métricas de avaliação de RAG e da análise. | Plano técnico (com avaliação) |
| DP-16 | Política de retentativas do LLM e das ferramentas (quantidade, backoff). | Plano técnico |
| DP-17 | Fonte e versionamento da tabela de preços para estimativa de custos. | Plano técnico |
| DP-19 | Ferramentas externas permitidas e seus destinos. | Especificação por ferramenta |
| DP-20 | Requisitos não funcionais (latência, volume, disponibilidade) e ambiente de implantação. | Especificação + plano |
| DP-21 | Retenção da base de conhecimento do RAG (documentos oficiais e versões antigas), já que o prazo de 30 dias foi definido para dados operacionais. | Esclarecimentos |
| DP-22 | Boletim diário: horário de envio, fuso, definição do "dia de referência" (data de publicação ou de captura), provedor de e-mail e gestão da lista de destinatários. | Especificação do boletim + plano |
| DP-23 | Retenção dos registros de consumo e custo: confirmar se o prazo de 30 dias atende às necessidades de auditoria financeira. | Esclarecimentos |
| DP-24 | Repositório normativo dos documentos coletados: o catálogo cita SharePoint ou Azure Blob Storage, que não fazem parte da arquitetura atual. Definir a tecnologia, o serviço proprietário (Api.RAG ou Api.Agente), a relação com a retenção de 30 dias. | Plano técnico + ADR |
| DP-25 | UFs e municípios monitorados (o catálogo usa `<UF>` e `<Municipio>` como modelo), e quais reguladores e tribunais de contas se aplicam a cada nicho. | Especificação de monitoramento |
| DP-26 | Vínculo entre os papéis do catálogo (AJ, AF, CC, TI) e os usuários do sistema, a revisão humana e o envio do boletim (ex.: se o CC aprova antes do envio). | Esclarecimentos |
| DP-27 | Quais APIs do catálogo serão usadas como ferramentas (INLABS/DOU, Senado, Câmara, LexML OAI-PMH, Querido Diário, DJEN, DataJud) e suas regras de acesso. Complementa DP-19. | Especificação por ferramenta |

**Version**: 2.1.0 | **Ratified**: 2026-10-29 | **Last Amended**: 2026-11-10

Alteração 2.1.0 (2026-11-10, MINOR, aprovada pela responsável do projeto): Princípio VII ganha a regra
das solicitações da SOFIA; DP-18 resolvida. Impacto: features 004 e 005 e ADR 0009; nenhuma
especificação anterior muda.
