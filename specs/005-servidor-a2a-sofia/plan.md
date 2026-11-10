# Plano: servidor A2A para a SOFIA

**Entrada**: [spec.md](spec.md)

## Decisões técnicas

- **Sem SDK A2A:** nenhum pacote A2A está no `Directory.Packages.props`; o envelope v0.3 é implementado
  com os `record`s da feature 004 (`ModelosA2A.cs`), cobertos por testes de contrato e ponta a ponta.
- **Reuso do chat:** `AnalisarSolicitacaoSofia` (Application) chama `ResponderMensagemConversa` com
  `conversaId = sofia-{idSolicitacao}` e usuário técnico `sofia-a2a`. Um parâmetro novo,
  `contextoNaoPersistido`, leva os documentos ao LLM sem gravá-los na memória nem na consulta.
- **Síncrono:** `message/send` devolve Task terminal; não há armazenamento de tarefas.

## Fluxo

```
POST /a2a (A2AEndpoints)
  ├─ JSON-RPC inválido / método / role / messageId ─────────▶ erro JSON-RPC
  ├─ MapeadorA2ASofia.LerSolicitacao (TextPart → contrato) ──▶ -32602 se fora do contrato
  ├─ AnalisarSolicitacaoSofia (timeout A2A:TimeoutSegundos)
  │    └─ ResponderMensagemConversa(pergunta, contextoNaoPersistido = documentos delimitados)
  │         └─ LLM + ferramentas buscar_novidades/obter_novidade + validação de citações
  ├─ Mapear: envelope → ResultadoAnaliseSofia (novidades → achados com links)
  ├─ MapeadorA2ASofia.CriarTarefaConcluida (valida o resultado) ─▶ Task completed + Artifact
  └─ timeout / exceção / resultado fora do contrato ────────▶ Task failed
```

## Mapeamento de status

| Envelope do chat | `ResultadoAnaliseSofia.status` |
|---|---|
| `concluido` | `concluido` |
| `concluido_com_limitacoes` | `parcial` (limitações em `observacoes`) |
| `falha_tecnica`, `rejeitado_validacao` | `erro` (`agente.falha_tecnica`, mensagem do chat) |

## Arquivos

| Arquivo | Mudança |
|---|---|
| `Agente.Application/Sofia/AnalisarSolicitacaoSofia.cs` | Novo: handler e mapeamento |
| `Agente.Application/Monitoramento/ResponderMensagemConversa.cs` | Parâmetro opcional `contextoNaoPersistido` |
| `Agente.Application/DependencyInjection.cs` | Registro do handler |
| `Api.Agente/A2A/A2AEndpoints.cs` | Novo: `POST /a2a` |
| `Api.Agente/A2A/AgentCard.cs` | `OpcoesA2A.TimeoutSegundos` |
| `Api.Agente/Program.cs`, `appsettings.json` | `MapA2A`, `A2A:TimeoutSegundos` |
| `tests/.../A2A/A2AServidorTests.cs` | Novo: ponta a ponta e protocolo |
| `.specify/memory/constitution.md` | Emenda 2.1.0 (Princípio VII, DP-18) |

## Docker e rede

A API não roda no Compose (só os bancos). Na Vercel, a SOFIA usa a URL pública HTTPS (`A2A__UrlPublica`).
Se a SOFIA e o agente forem colocados numa rede Docker, a SOFIA deve chamar o nome do serviço
(`http://agente-monitoramento-legislacao:8080/a2a`) e `A2A:UrlPublica` deve apontar para esse endereço.
