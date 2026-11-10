# Especificação: contrato SOFIA v1.0 e Agent Card A2A

- **Feature**: 004-contrato-sofia-a2a
- **Data**: 2026-11-10
- **Origem**: skill `sofia-a2a-contrato` (SOFIA/INFIS); decisões no [ADR 0009](../../docs/adr/0009-contrato-sofia-a2a.md)

## Objetivo

Fixar o contrato de negócio entre a SOFIA e este agente e publicar o Agent Card, para que a próxima
etapa (servidor A2A, skill `sofia-a2a-implementacao`) só ligue o transporte ao caso de uso.

## Fora do escopo

- Endpoint `POST /a2a` e o tratamento de `message/send` (próxima etapa).
- Mapeamento da pergunta para a busca de novidades (próxima etapa).
- Autenticação (ADR 0007).

## O que é A2A e o que é contrato SOFIA

| Camada | Campos | Dono |
|---|---|---|
| JSON-RPC 2.0 | `jsonrpc`, `id`, `method`, `params`, `result`, `error` | Protocolo |
| A2A v0.3 | `message.role`, `messageId`, `parts[].kind/text`, Task `id`, `contextId`, `status.state`, `artifacts[].artifactId/name/parts` | Protocolo |
| Contrato SOFIA v1.0 (dentro do `text`) | `versaoContrato`, `idExecucao`, `idSolicitacao`, `usuario.nome`, `pergunta`, `documentos[]`, `contexto`; na volta `agente`, `status`, `analise`, `achados[]`, `observacoes`, `erro` | Turma SOFIA |
| Agent Card | `protocolVersion`, `name`, `url`, `preferredTransport`, `capabilities`, `skills[]` | Protocolo, publicado pelo agente |

## Requisitos

- **FR-401** Aceitar `SolicitacaoAnaliseSofia` v1.0: versão `1.0`; `idExecucao` e `idSolicitacao` UUID;
  `usuario.nome`, `pergunta` e `documentos` (≥ 1) obrigatórios; `idDocumento`, `nome` e `texto` não
  vazios; `idDocumento` único; `tipo` e `contexto` opcionais.
- **FR-402** Recusar, sem truncar, pergunta e documentos acima dos limites de `Sofia:*`.
- **FR-403** Tolerar campos desconhecidos (evolução compatível da v1).
- **FR-404** Responder `ResultadoAnaliseSofia` com os mesmos `idExecucao` e `idSolicitacao`, `agente.idAgente
  = agente-monitoramento-legislacao`, `status` em `concluido|parcial|erro`, `analise` sempre preenchida
  e `achados` (pode ser `[]`).
- **FR-405** `status = erro` exige `erro.codigo` e `erro.mensagem` não sensíveis; outros status não têm `erro`.
- **FR-406** `achados[].idDocumentos` só cita documentos da solicitação; `severidade` em
  `baixa|media|alta|critica`; evidências são links e trechos oficiais reais, nunca inventados.
- **FR-407** A2A: exatamente uma `TextPart` com o JSON do pedido; resposta como Task `completed` com o
  Artifact `analise-sofia-v1`. Task em andamento ou `failed` não é resultado de análise.
- **FR-408** Publicar o Agent Card A2A v0.3 em `GET /.well-known/agent-card.json`, com `url` de
  `A2A:UrlPublica` ou, sem ela, derivada do host do pedido + `/a2a`.
- **FR-409** Mensagens de erro não repetem nome do usuário nem texto de documento.

## Critérios de aceite

1. Os exemplos em [contracts/exemplos](contracts/exemplos) validam nos schemas e no validador.
2. Cada campo obrigatório ausente ou vazio gera erro com código estável (`contrato.*`, `a2a.*`).
3. Round-trip JSON e round-trip A2A (resultado → Task → resultado) preservam ids e achados.
4. O Agent Card valida em [agent-card.schema.json](contracts/agent-card.schema.json).
