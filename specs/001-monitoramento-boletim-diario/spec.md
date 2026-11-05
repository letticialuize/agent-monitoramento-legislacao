# Feature Specification: Monitoramento de novidades e boletim diário

**Feature Branch**: `001-monitoramento-boletim-diario`

**Created**: 2026-10-29

**Status**: Draft

**Input**: User description: "Monitoramento de legislação e jurisprudência com consulta sob demanda
por assunto e boletim diário por e-mail, conforme constitution v1.2.0 (Princípios II, VI, VII,
VIII, XVI, XIX e seção 4.4) e catálogo de fontes em
docs/fontes/catalogo-fontes-confiaveis-2026-10-29.pdf. O agente busca novidades nas fontes do
catálogo nas três esferas e nos grupos de tributos, tribunais judiciais, administrativos e de
contas; classifica cada novidade por esfera e por perfil; quando a consulta envolve um cliente
(somente nome e nicho), cruza as novidades com o nicho; novidades de fonte fora do catálogo ou só
de agregador exigem revisão humana e não entram como confirmadas; entrega sob demanda na resposta
do agente e boletim diário por e-mail para lista de funcionários só com as novidades do dia,
agrupado por esfera e perfil, idempotente por dia."

## Clarifications

### Session 2026-10-29

- Q: O "dia do boletim" é o dia de publicação ou de coleta, e em qual horário o boletim chega? →
  A: Dia de **publicação**. O boletim é enviado em dias úteis, às 8h, com as publicações do dia
  útil anterior.
- Q: O boletim precisa de aprovação antes do envio? → A: Envio automático, mas novidades
  pendentes (só em agregador ou de fonte não catalogada) só entram no boletim depois de revisadas
  e confirmadas.
- Q: Quais UFs e municípios entram na primeira versão? → A: Federal mais uma lista de UFs e uma
  lista de municípios.
- Q: Em que ordem a feature é entregue? → A: Em três entregas: (1) chat e consulta de novidades
  classificadas por perfil, sem cliente e sem e-mail (US1); (2) cadastro de empresa e nicho e
  consulta das novidades que impactam o nicho (US3 + cadastro de clientes); (3) agendamento das 8h
  e cadastro dos funcionários destinatários (US2 + destinatários). Fila de revisão (US4) e
  administração do catálogo e entes por API (restante da US5) ficam para depois; até lá, o
  catálogo vem da carga inicial e pendências ficam só marcadas para revisão humana.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consulta sob demanda por assunto (Priority: P1)

Um consultor tributário ou um desenvolvedor informa ao agente um assunto (ex.: "NF-e",
"substituição tributária ICMS", "CBS") e, opcionalmente, um período, esferas e perfil. O agente
procura novidades nas fontes do catálogo e devolve uma lista de novidades classificadas por esfera
e por perfil, cada uma com ementa, órgão emissor, tipo e número do ato, data de publicação, link
oficial e situação (confirmada, pendente de confirmação ou fonte não catalogada).

**Why this priority**: É a menor fatia que entrega valor sozinha: sem ela não há novidades para o
boletim nem para o cruzamento com clientes. Também exercita todas as regras centrais (catálogo,
classificação, evidência e limitações).

**Independent Test**: Enviar um assunto com novidades conhecidas em fontes do catálogo e conferir
que a resposta traz essas novidades classificadas e com link oficial; enviar um assunto sem
novidades e conferir que a resposta declara a ausência e as fontes consultadas.

**Acceptance Scenarios**:

1. **Given** um ato publicado no DOU dentro do período consultado sobre o assunto informado,
   **When** o usuário consulta o assunto, **Then** a resposta lista o ato com esfera "federal",
   perfil atribuído, ementa, tipo e número, data de publicação, link do DOU e situação
   "confirmada".
2. **Given** uma nota técnica de leiaute de NF-e publicada no período, **When** o usuário consulta
   "NF-e", **Then** a novidade é atribuída ao perfil desenvolvedor; se também alterar obrigação
   tributária, é atribuída aos dois perfis.
3. **Given** que nenhuma fonte consultada tem novidade sobre o assunto, **When** o usuário
   consulta, **Then** a resposta informa "sem novidades encontradas", lista as fontes consultadas e
   NÃO afirma que não houve mudança normativa.
4. **Given** que uma fonte do catálogo está fora do ar, **When** o usuário consulta, **Then** a
   resposta informa a falha daquela fonte como limitação e a resposta não é apresentada como
   completa.
5. **Given** uma novidade encontrada apenas em agregador (ex.: Querido Diário), **When** o
   usuário consulta, **Then** a novidade aparece como "pendente de confirmação", separada das
   confirmadas, até ser localizada no diário oficial de origem.
6. **Given** uma novidade encontrada em base fora do catálogo, **When** o usuário consulta,
   **Then** a novidade aparece como "fonte não catalogada", marcada para revisão humana e sem
   conclusão de impacto.

---

### User Story 2 - Boletim diário por e-mail (Priority: P3)

Em todo dia útil, às 8h, os funcionários cadastrados na lista de destinatários recebem por e-mail
um boletim com as novidades publicadas no dia útil anterior (dia de referência), agrupadas por esfera (federal, estadual, municipal) e por
perfil (consultor tributário, desenvolvedor), com resumo executivo curto e links oficiais.

**Why this priority**: É o principal canal de consumo da equipe, mas depende da busca e da
classificação da US1.

**Independent Test**: Simular um dia com novidades conhecidas e disparar o boletim; conferir o
conteúdo, o agrupamento e que um segundo disparo para o mesmo dia não gera novo envio.

**Acceptance Scenarios**:

1. **Given** novidades confirmadas com data de publicação no dia de referência, **When** o
   boletim é gerado, **Then** ele contém somente essas novidades, agrupadas por esfera e perfil,
   cada uma com ementa, órgão, ato, data e link oficial.
2. **Given** novidades publicadas em dias anteriores, **When** o boletim do dia é gerado,
   **Then** elas NÃO aparecem como novidades do dia.
3. **Given** que o boletim do dia já foi enviado para a lista, **When** o envio é disparado de
   novo, **Then** nenhum e-mail duplicado é enviado e a tentativa fica registrada.
4. **Given** um dia sem novidades, **When** o boletim é gerado, **Then** os destinatários recebem
   um boletim informando "sem novidades no dia" e as fontes verificadas.
5. **Given** novidades pendentes de confirmação ou de fonte não catalogada ainda não revisadas,
   **When** o boletim é gerado, **Then** elas NÃO aparecem no boletim.
6. **Given** uma novidade pendente que foi confirmada na revisão humana depois do boletim do seu
   dia de publicação, **When** o próximo boletim é gerado, **Then** ela aparece uma única vez em
   seção "confirmadas após revisão", com a data real de publicação.
7. **Given** que segunda-feira é o dia útil seguinte a sexta, sábado e domingo, **When** o
   boletim de segunda é gerado, **Then** ele inclui as publicações de sexta, sábado e domingo
   (o mesmo vale para feriados).
8. **Given** falha do serviço de e-mail, **When** o envio é tentado, **Then** a falha fica
   registrada como falha técnica, com nova tentativa conforme política, e o boletim não é marcado
   como enviado.
9. **Given** que alguma fonte de coleta diária falhou no dia, **When** o boletim é gerado,
   **Then** o boletim informa quais fontes não puderam ser verificadas.

---

### User Story 3 - Novidades relevantes para um cliente (Priority: P2)

Um consultor informa o nome de um cliente cadastrado e o agente devolve as novidades do período
relevantes para o nicho de negócio desse cliente, explicando por que cada novidade se relaciona
ao nicho.

**Why this priority**: Agrega valor de consultoria, mas depende da US1 e do cadastro de clientes.

**Independent Test**: Cadastrar um cliente do nicho "Venda de produtos farmacêuticos", ter
novidades conhecidas sobre medicamentos e sobre outro setor, consultar o cliente e conferir que só
as relacionadas ao nicho aparecem, com a justificativa.

**Acceptance Scenarios**:

1. **Given** um cliente cadastrado com nicho "Venda de produtos farmacêuticos", **When** o
   consultor consulta novidades para esse cliente, **Then** a resposta lista as novidades
   relacionadas ao nicho, cada uma com a justificativa da relação e a evidência.
2. **Given** que a relação de uma novidade com o nicho foi inferida pela IA, **When** a resposta
   é gerada, **Then** essa relação é identificada como "indício identificado pela IA" e indica
   revisão humana.
3. **Given** uma consulta com dados do cliente além de nome e nicho (ex.: CNPJ, faturamento),
   **When** a consulta é recebida, **Then** os dados extras são rejeitados e não são usados nem
   armazenados.
4. **Given** um cliente não cadastrado ou com nicho fora da lista, **When** a consulta é feita,
   **Then** o agente retorna erro de validação e não executa a busca.

---

### User Story 4 - Revisão humana de novidades não confirmadas (Priority: P4)

Um revisor vê a fila de novidades pendentes (fonte não catalogada ou só de agregador) e marca cada
uma como confirmada (informando o link oficial), descartada ou mantida em análise.

**Why this priority**: Garante o controle de qualidade exigido pela constitution, mas as US1–US3
já funcionam marcando as pendências.

**Independent Test**: Gerar uma novidade de fonte não catalogada, confirmá-la com link oficial e
verificar que passa a aparecer como confirmada nas consultas e no boletim seguinte.

**Acceptance Scenarios**:

1. **Given** uma novidade pendente, **When** o revisor a confirma informando o link da
   publicação oficial, **Then** ela passa a "confirmada" com registro de quem revisou e quando.
2. **Given** uma novidade pendente, **When** o revisor a descarta, **Then** ela não aparece em
   consultas nem boletins futuros, e o motivo fica registrado.
3. **Given** uma confirmação sem link de fonte oficial, **When** o revisor tenta confirmar,
   **Then** a confirmação é recusada.

---

### User Story 5 - Administração do catálogo, clientes e destinatários (Priority: P5)

Um administrador mantém o catálogo de fontes (carga inicial a partir do documento oficial do
projeto), o cadastro de clientes (somente nome e nicho) e a lista de destinatários do boletim.

**Why this priority**: Necessário para operar, mas a carga inicial pode ser feita uma vez a partir
do catálogo aprovado.

**Independent Test**: Carregar o catálogo inicial, cadastrar um cliente e um destinatário, e
verificar que consultas e boletim usam esses dados.

**Acceptance Scenarios**:

1. **Given** o catálogo aprovado de 2026-10-29, **When** a carga inicial é feita, **Then** cada
   fonte fica registrada com os campos obrigatórios da seção 4.4 da constitution e com versão do
   catálogo.
2. **Given** uma fonte sem domínio oficial ou sem órgão emissor, **When** o administrador tenta
   incluí-la como confiável, **Then** a inclusão é recusada, salvo se marcada como agregador de
   apoio.
3. **Given** um cadastro de cliente com campos além de nome e nicho, **When** é enviado, **Then**
   é recusado.
4. **Given** um destinatário removido da lista, **When** o próximo boletim é enviado, **Then** ele
   não o recebe.

### Edge Cases

- O mesmo ato aparece em várias fontes (DOU, Planalto, LexML): deve ser apresentado uma vez, com
  a publicação oficial como evidência principal e as demais como referências.
- Ato republicado ou retificado: deve aparecer vinculado ao ato original, identificado como
  retificação.
- Fonte com periodicidade semanal ou quinzenal: novidades coletadas depois do boletim do seu dia
  de publicação não entram em boletins posteriores; aparecem nas consultas com a data real.
- Feriados e fins de semana: não há boletim; o boletim do dia útil seguinte cobre as publicações
  do intervalo. Ausência de publicação nesses dias não é falha.
- Feriados estaduais ou municipais: o dia útil considerado é o nacional; ausência de publicação
  local não é falha.
- Fonte que muda de endereço: deve ser tratada como falha da fonte até o catálogo ser atualizado.
- Conteúdo de publicação com texto que tenta instruir o agente: deve ser tratado apenas como dado.
- Novidade que se aplica a vários estados ou municípios: deve indicar cada ente afetado.
- Assunto muito genérico na consulta (ex.: "imposto"): a resposta deve limitar a quantidade de
  itens e informar que houve corte.
- Dados operacionais com mais de 30 dias: devem ser excluídos, exceto o que estiver em revisão
  humana aberta.

## Requirements *(mandatory)*

### Functional Requirements

**Fontes e coleta**

- **FR-001**: O sistema DEVE buscar novidades nas fontes do catálogo de fontes confiáveis,
  respeitando a periodicidade de coleta definida para cada tipo de fonte no catálogo.
- **FR-002**: O sistema DEVE registrar, para cada novidade, os metadados: data de publicação,
  órgão emissor, tipo e número do ato, ementa, link da fonte oficial, tema, impacto (alto, médio,
  baixo), esfera, ente federativo quando houver, grupo do catálogo, áreas ou clientes afetados e
  responsável interno da fonte.
- **FR-003**: O sistema DEVE classificar cada novidade em uma situação: "confirmada" (encontrada
  em fonte do catálogo com validade jurídica ou base oficial), "pendente de confirmação" (só em
  agregador) ou "fonte não catalogada".
- **FR-004**: O sistema DEVE marcar para revisão humana toda novidade "pendente de confirmação"
  ou de "fonte não catalogada".
- **FR-005**: O sistema DEVE consolidar o mesmo ato encontrado em várias fontes em uma única
  novidade, usando a publicação com validade jurídica como evidência principal.
- **FR-006**: O sistema DEVE registrar falhas de acesso a fontes como falha técnica, identificando
  a fonte, sem tratá-las como ausência de novidades.

**Classificação**

- **FR-007**: O sistema DEVE classificar cada novidade por esfera (federal, estadual, municipal).
- **FR-008**: O sistema DEVE atribuir cada novidade ao perfil desenvolvedor quando tratar de
  layout, adequação de sistemas ou atualização de API (incluindo schemas e notas técnicas), e ao
  perfil consultor tributário quando tratar de especificações da legislação ou notícias sobre
  legislação; a novidade que atender aos dois critérios DEVE receber os dois perfis.
- **FR-009**: O sistema DEVE registrar a origem de cada classificação (regra determinística ou
  indício identificado pela IA).

**Consulta sob demanda**

- **FR-010**: Usuários DEVEM poder consultar novidades por assunto, com filtros opcionais de
  período, esfera, perfil e grupo do catálogo.
- **FR-011**: A resposta da consulta DEVE trazer as novidades agrupadas por situação, esfera e
  perfil, as fontes consultadas, as fontes que falharam e as limitações da busca.
- **FR-012**: Quando não houver novidades, a resposta DEVE informar "sem novidades encontradas"
  com as fontes consultadas e NÃO DEVE afirmar ausência de mudança normativa.
- **FR-013**: A resposta DEVE indicar se requer revisão humana, conforme os critérios do
  Princípio XVI da constitution.

**Cruzamento com cliente**

- **FR-014**: Usuários DEVEM poder consultar novidades para um cliente cadastrado, identificado
  apenas pelo nome.
- **FR-015**: O sistema DEVE usar somente o nome e o nicho do cliente; DEVE rejeitar qualquer
  outro dado do cliente recebido.
- **FR-016**: O sistema DEVE aceitar apenas os nichos "Venda de mercadorias de varejo", "Venda de
  produtos farmacêuticos" e "Tributação padrão".
- **FR-017**: Para cada novidade relacionada ao cliente, o sistema DEVE informar a justificativa
  da relação com o nicho e se ela é regra determinística ou indício da IA.

**Boletim diário**

- **FR-018**: O sistema DEVE gerar, uma vez por dia de referência, um boletim com as novidades
  confirmadas publicadas nesse dia, agrupadas por esfera e por perfil.
- **FR-019**: O boletim DEVE ser enviado por e-mail a todos os destinatários ativos da lista.
- **FR-020**: O envio DEVE ser idempotente por dia de referência e lista: um novo disparo para o
  mesmo dia NÃO DEVE gerar e-mails duplicados.
- **FR-021**: Novidades pendentes de confirmação ou de fonte não catalogada NÃO DEVEM aparecer no
  boletim enquanto não forem confirmadas na revisão humana. Uma novidade confirmada na revisão
  depois do boletim do seu dia de publicação DEVE aparecer uma única vez no boletim seguinte, em
  seção "confirmadas após revisão", com a data real de publicação.
- **FR-022**: Um dia sem novidades DEVE gerar boletim informando "sem novidades no dia".
- **FR-023**: O boletim DEVE informar as fontes de coleta diária que não puderam ser verificadas
  no dia.
- **FR-024**: O boletim DEVE conter um resumo executivo do dia baseado apenas nas novidades
  listadas.
- **FR-025**: O boletim DEVE ser enviado em dias úteis, às 8h (horário de Brasília). O dia de
  referência é a **data de publicação** do ato: cada boletim cobre as publicações desde o último
  dia coberto até o dia útil anterior, incluindo fins de semana e feriados intermediários.
  Novidades publicadas nesse intervalo, mas coletadas depois do envio, NÃO DEVEM entrar em
  boletins posteriores como novidades do dia; ficam disponíveis nas consultas sob demanda.
- **FR-026**: O boletim DEVE ser enviado automaticamente, sem aprovação prévia, respeitando a
  FR-021.

**Revisão humana**

- **FR-027**: Revisores DEVEM poder confirmar, descartar ou manter em análise uma novidade
  pendente, com registro de autor, data e motivo.
- **FR-028**: A confirmação DEVE exigir o link da publicação oficial.

**Administração**

- **FR-029**: Administradores DEVEM poder carregar e atualizar o catálogo de fontes, com versão,
  a partir do documento oficial do projeto.
- **FR-030**: O sistema DEVE recusar como fonte confiável a fonte que não atenda aos critérios de
  confiabilidade da constitution; agregadores DEVEM ser registrados como "apoio".
- **FR-031**: Administradores DEVEM poder manter o cadastro de clientes (nome e nicho) e a lista
  de destinatários do boletim.

**Abrangência inicial**

- **FR-032**: Esta funcionalidade DEVE cobrir as fontes federais e nacionais do catálogo e as
  fontes estaduais e municipais de uma lista de UFs monitoradas e de uma lista de municípios
  monitorados.
- **FR-035**: Administradores DEVEM poder manter as listas de UFs e de municípios monitorados;
  incluir uma UF ou município DEVE exigir o registro das respectivas fontes do catálogo (diário
  oficial, legislativo, fazenda e demais aplicáveis). Um ente sem fontes registradas NÃO DEVE ser
  apresentado como monitorado.

**Rastreabilidade e retenção**

- **FR-033**: Toda consulta e todo boletim DEVEM ter identificador de execução e registro das
  fontes consultadas, falhas, novidades retornadas e consumo.
- **FR-034**: Dados operacionais (consultas, novidades coletadas, boletins, histórico) DEVEM ser
  excluídos após 30 dias, exceto itens em revisão humana aberta, o catálogo e o cadastro de
  clientes.

### Key Entities *(include if feature involves data)*

- **Fonte**: item do catálogo (nome, link, o que monitorar, órgão emissor, esfera, grupo, tipo:
  oficial ou agregador de apoio, responsável interno, formato, periodicidade, versão do catálogo).
- **Novidade**: ato ou decisão identificado (metadados da FR-002, situação, perfis, origem da
  classificação, fontes onde foi encontrado, vínculo com ato original quando for retificação).
- **Consulta**: pedido sob demanda (assunto, filtros, cliente opcional, usuário, resultado,
  limitações, execução).
- **Cliente**: nome e nicho, apenas.
- **Nicho**: valor de uma lista fechada e versionada.
- **Boletim**: edição diária (dia de referência, novidades incluídas, resumo, fontes não
  verificadas, situação de envio).
- **Destinatário**: funcionário da lista do boletim (nome, e-mail, ativo/inativo).
- **Revisão**: decisão humana sobre uma novidade pendente (revisor, data, decisão, motivo, link
  oficial).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das novidades apresentadas como confirmadas, em consultas e boletins, têm link
  para a publicação oficial de uma fonte do catálogo.
- **SC-002**: 0 novidades de fonte não catalogada ou só de agregador aparecem como confirmadas
  sem revisão humana registrada.
- **SC-003**: 0 boletins duplicados para o mesmo dia de referência e lista.
- **SC-004**: Em uma amostra de validação revisada por especialistas, ao menos 90% das novidades
  recebem esfera e perfil corretos.
- **SC-005**: Em uma amostra de dias com novidades conhecidas, ao menos 95% das novidades
  confirmadas publicadas nas fontes de coleta diária aparecem no boletim do dia correspondente.
- **SC-009**: Em 95% dos dias úteis, o boletim chega aos destinatários até as 8h15.
- **SC-006**: 95% das consultas sob demanda retornam resultado em até 2 minutos.
- **SC-007**: 100% das falhas de fonte em um dia aparecem como limitação na consulta ou no
  boletim correspondente.
- **SC-008**: Após 30 dias de uso, ao menos 80% dos destinatários consultados avaliam o boletim
  como útil para o trabalho.

## Assumptions

- Escala de severidade de inconsistências e nomes técnicos das categorias ficam para outra
  especificação (DP-07); esta funcionalidade usa apenas a escala de impacto do catálogo.
- Os usuários do agente (consultores, desenvolvedores, revisores e administradores) são
  funcionários internos. **Hoje a API não tem autenticação (desvio temporário, ADR 0007)**; o mecanismo será definido depois (DP-13).
- O tempo de resposta de 2 minutos (SC-006) é uma meta inicial, a confirmar nos requisitos não
  funcionais (DP-20).
- A relação "novidade × nicho" pode exigir inferência da IA; nesse caso é tratada como indício
  com revisão humana, conforme a constitution.
- Quais APIs das fontes serão usadas para coleta é decisão do plano técnico (DP-27).
- Análise de impacto tributário detalhada e análise de jurisprudência aprofundada ficam para
  especificações próprias; aqui o resumo executivo é curto e baseado só nas novidades listadas.
- Regulador e tribunal de contas aplicáveis por nicho serão tratados na especificação do
  cruzamento detalhado (DP-25).
- Os valores iniciais das listas de UFs e municípios monitorados serão informados pelo
  administrador antes da entrada em produção; a funcionalidade não depende de quais são.
- O calendário de dias úteis considera os feriados nacionais.
