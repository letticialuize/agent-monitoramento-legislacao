# Ferramentas expostas ao LLM (Semantic Kernel / Function Calling)

Usadas apenas no endpoint de conversa (`/api/v1/conversas/{id}/mensagens`). A consulta estruturada
(`/api/v1/consultas`) não depende do LLM para escolher ferramentas: executa o fluxo diretamente.

Regras comuns (Princípio XII):

- Todas são **somente leitura**; nenhuma altera estado, envia e-mail ou revisa novidades.
- **Autorização** (hoje com usuário fixo que tem esses papéis: desvio no ADR 0007): antes de executar, o sistema verifica o papel do usuário da conversa
  (`consultor` ou `desenvolvedor`). A escolha da ferramenta pelo LLM não autoriza nada.
- **Validação de argumentos**: os mesmos validadores da `ConsultaRequest`; argumento inválido
  retorna erro estruturado ao LLM, sem execução.
- **Timeout**: configurável por ferramenta (padrão inicial 30 s); estouro → `falha_tecnica`.
- **Retentativa**: nenhuma dentro da ferramenta (consultas ao banco local); idempotentes.
- **Registro**: `EtapaExecucao` com etapa `ferramentas`, nome, argumentos (sem dados sensíveis),
  duração e status.
- **Saída**: o retorno é dado; o LLM só pode citar novidades e URLs presentes no retorno
  (verificado na validação da resposta).

| Nome | Responsabilidade | Entrada | Saída | Testes mínimos |
|---|---|---|---|---|
| `buscar_novidades` | Buscar novidades por período e, opcionalmente, assunto e filtros | `assunto?`, `periodo?`, `esferas?`, `perfis?` (se omitido, `consultor_tributario`), `grupos?` | lista de `novidade` (formato do envelope), `fontesComFalha`, `itensOmitidos` | argumentos inválidos; papel ausente; sem resultados |
| `buscar_novidades_cliente` | Mesmo que acima, cruzando com o nicho do cliente | `clienteNome`, `assunto?`, `periodo?` | lista de `novidade` com `relacaoNicho` | cliente inexistente; dados extras de cliente rejeitados |
| `obter_novidade` | Detalhar uma novidade e suas evidências | `novidadeId` | `novidade` | id inexistente; novidade descartada não retornada |
