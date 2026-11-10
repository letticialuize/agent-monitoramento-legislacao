# Especificação: servidor A2A para a SOFIA

- **Feature**: 005-servidor-a2a-sofia
- **Data**: 2026-11-10
- **Origem**: skill `sofia-a2a-implementacao`; contrato na [feature 004](../004-contrato-sofia-a2a/spec.md); [ADR 0009](../../docs/adr/0009-contrato-sofia-a2a.md); constitution 2.1.0

## Papéis

A SOFIA é cliente e orquestradora: descobre os agentes pelo Agent Card, despacha, agrega e sintetiza.
Este agente é **só servidor A2A**. Cliente A2A, registry, despacho paralelo e agregação ficam no
repositório da SOFIA, fora deste projeto.

## Requisitos

- **RF01** Agent Card em `GET /.well-known/agent-card.json` anunciando `POST /a2a`, transporte JSONRPC, A2A 0.3.0 (feature 004).
- **RF02** `POST /a2a` aceita JSON-RPC 2.0 `message/send` com `message.role = user`, `messageId` e uma TextPart com `SolicitacaoAnaliseSofia` v1.0.
- **RF03** Resposta: Task `completed` com o Artifact `analise-sofia-v1` contendo `ResultadoAnaliseSofia`, com os mesmos `idExecucao` e `idSolicitacao`; `contextId` = `message.contextId` ou `idExecucao`.
- **RF04** Ausência de novidades = `concluido`/`parcial` com `achados: []`; falha técnica (LLM indisponível, citação não verificável) = `status: erro` com `erro.codigo = agente.falha_tecnica`. Tempo limite ou exceção = Task `failed` sem detalhes internos.
- **RF05** Erros de protocolo viram erro JSON-RPC (HTTP 200): `-32700` JSON inválido, `-32600` requisição inválida, `-32601` método não suportado, `-32602` mensagem ou contrato inválidos. A mensagem não repete nome do usuário nem texto de documento.
- **RF06** A pergunta passa pelo mesmo fluxo do chat (ferramentas somente leitura, validação de citações, registro de consumo), conforme a Seção 3.3.
- **RF07** Cada novidade vira um achado com os links oficiais em `evidencias`; `idDocumentos` fica vazio (o agente não liga novidade a trecho de documento) e `severidade` nula (DP-07).
- **RF08** O agente não chama outros agentes.
- **RNF01** Documentos vão ao LLM delimitados como dado não confiável; documentos e nome do usuário não são persistidos, logados nem enviados ao LLM (no caso do nome) — constitution 2.1.0, Princípio VII.
- **RNF02** Tempo limite configurável (`A2A:TimeoutSegundos`, padrão 120 s); cancelamento quando o cliente desiste.
- **RNF03** Logs só com `idExecucao`, `idSolicitacao`, status, duração e códigos de erro; telemetria pelo OpenTelemetry já configurado.

## Critérios Given/When/Then

1. Dado o card e o endpoint, quando a SOFIA lê o card e chama o `url`, então recebe Task `completed` com Artifact válido no schema e os mesmos ids.
2. Dados dois documentos, quando a análise é enviada, então ambos chegam ao LLM delimitados e identificados por `idDocumento`, e nenhum é gravado no Mongo, no Postgres ou nos logs.
3. Dado um documento com injeção de prompt, quando o modelo tenta uma ferramenta não oferecida, então nada é executado e a análise segue.
4. Dado LLM indisponível ou resposta com link inventado, então `status: erro` com código não sensível.
5. Dado tempo limite esgotado, então Task `failed`.

## Fora do escopo / riscos

- Autenticação: resolvida na [feature 006](../006-autenticacao-a2a-sofia/spec.md) (Bearer `SOFIA_A2A_TOKEN`, ADR 0010).
- Deduplicação por `idSolicitacao` em retries da SOFIA: não implementada (hospedagem serverless sem estado compartilhado); um retry refaz a análise.
- `tasks/get`, streaming e push notifications não são suportados (o card declara isso).
