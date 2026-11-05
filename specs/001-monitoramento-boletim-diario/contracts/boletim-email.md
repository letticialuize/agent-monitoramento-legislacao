# Contrato do e-mail do boletim diário (v1)

**Assunto**: `Boletim normativo — <dd/mm/aaaa do dia útil de envio>` (sem novidades:
`Boletim normativo — <data> — sem novidades`).

**Formato**: HTML com alternativa em texto puro. Nenhum conteúdo coletado é inserido como HTML
bruto: ementas são codificadas como texto.

**Estrutura (ordem fixa)**:

1. Cabeçalho: intervalo coberto (`diaReferenciaInicio` a `diaReferenciaFim`) e identificador do
   boletim (`execucaoId`).
2. Resumo executivo (opcional): texto validado de `llm-resumo-boletim`, identificado como
   "gerado por IA a partir das novidades abaixo". Omitido se a geração falhar.
3. Novidades confirmadas, agrupadas por **esfera** (Federal, Estadual, Municipal) e, dentro de
   cada esfera, por **perfil** (Consultor tributário, Desenvolvedor). Uma novidade com dois perfis
   aparece nos dois grupos. Cada item: data de publicação, órgão, tipo e número do ato, ente
   (quando houver), ementa, impacto (com marcação "sugerido por IA" quando for indício), link
   oficial.
4. Confirmadas após revisão (só se houver): mesma estrutura, com a data real de publicação.
5. Fontes não verificadas no intervalo (só se houver): nome da fonte e motivo resumido.
6. Rodapé: aviso de que o boletim não substitui análise jurídica/tributária e contato da equipe.

**Sem novidades**: seções 3 e 4 substituídas por "Sem novidades no período nas fontes
verificadas", seguida da lista de fontes verificadas; seção 5 continua obrigatória se houver
falha.

**Nunca inclui**: novidades `pendente_confirmacao`, `fonte_nao_catalogada`, `em_analise` ou
`descartada`; dados de clientes.
