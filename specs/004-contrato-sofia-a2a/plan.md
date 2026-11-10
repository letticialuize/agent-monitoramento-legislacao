# Plano: contrato SOFIA v1.0 e Agent Card A2A

**Entrada**: [spec.md](spec.md), [ADR 0009](../../docs/adr/0009-contrato-sofia-a2a.md)

## Situação encontrada

- Sem SDK A2A no repositório: o envelope é modelado com `record`s próprios (subconjunto v0.3 usado com a SOFIA).
- `JsonContrato` (camelCase, enums snake_case) já é o padrão dos contratos; `JsonSchema.Net` já é usado
  nos testes de contrato.
- `Resultado`/`ErroDominio` (Domain) já são o padrão de erro previsível.

## Fluxo (próxima etapa liga o transporte)

```
SOFIA ── POST /a2a  JSON-RPC message/send ──▶ RequisicaoJsonRpc
          MapeadorA2ASofia.LerSolicitacao      (TextPart → JSON → SolicitacaoAnaliseSofia → ValidadorContratoSofia)
          caso de uso de novidades             (pergunta + documentos como dado não confiável) ── próxima etapa
          ResultadoAnaliseSofia                (mesmos ids, agente, status, achados com links oficiais)
          MapeadorA2ASofia.CriarTarefaConcluida (ValidarResultado → Artifact analise-sofia-v1 → Task completed)
SOFIA ◀── RespostaJsonRpc { result: Task } ──
```

Erro de contrato na entrada: resposta JSON-RPC de erro (`-32602 Invalid params`), sem eco do conteúdo.
Falha de negócio (ex.: LLM indisponível): Task `completed` com `status: erro` (`ResultadoAnaliseSofia.Falha`).
Falha grave/inesperada: Task `failed` sem detalhes sensíveis.

## Estrutura

| Camada | Arquivo | Conteúdo |
|---|---|---|
| Application | `Sofia/ContratoSofia.cs` | DTOs, constantes de status/severidade, `OpcoesContratoSofia` |
| Application | `Sofia/ValidadorContratoSofia.cs` | Regras de pedido e resposta |
| Api | `A2A/ModelosA2A.cs` | JSON-RPC, Message, Part, Task, Artifact; `ProtocoloA2A.Json` |
| Api | `A2A/MapeadorA2ASofia.cs` | A2A ↔ contrato |
| Api | `A2A/AgentCard.cs` | Agent Card, `OpcoesA2A`, DI e `GET /.well-known/agent-card.json` |
| specs | `contracts/*.schema.json`, `contracts/exemplos/*.json` | Schemas e fixtures |

## Configuração

| Chave | Padrão | Uso |
|---|---|---|
| `Sofia:IdAgente` | `agente-monitoramento-legislacao` | `agente.idAgente` e Agent Card |
| `Sofia:NomeAgente` | Agente de Monitoramento da Legislação e Jurisprudência | `agente.nome` e `name` |
| `Sofia:MaxCaracteresPergunta` | 4000 | Limite da pergunta |
| `Sofia:MaxDocumentos` | 20 | Limite de documentos |
| `Sofia:MaxCaracteresPorDocumento` | 200000 | Limite por documento |
| `Sofia:MaxCaracteresTotalDocumentos` | 500000 | Limite somado |
| `A2A:UrlPublica` | vazio (deriva do pedido) | `url` do Agent Card; definir na Vercel, que fica atrás de proxy |
| `A2A:VersaoAgente` | `1.0.0` | `version` do Agent Card |

## Riscos

- O Agent Card anuncia `/a2a` antes de o endpoint existir (decisão da responsável).
- Sem `A2A:UrlPublica` na Vercel, o `url` pode sair `http://` por causa do proxy.
- Princípio VII × documentos da SOFIA: emenda pendente (ADR 0009, T412).
