# Plano: autenticação A2A e cadastro na SOFIA

**Entrada**: [spec.md](spec.md)

## Implementação

| Arquivo | Mudança |
|---|---|
| `Api.Agente/A2A/AutenticacaoSofia.cs` | Novo: filtro `RequireTokenSofia` (503 sem configuração, 401 + `WWW-Authenticate`) |
| `Api.Agente/A2A/AgentCard.cs` | Filtro no card; `text/plain`; skill `analisar_documentos`; `securitySchemes` e `security` |
| `Api.Agente/A2A/A2AEndpoints.cs` | Filtro no RPC; `tasks/get`, `tasks/cancel`; Task guardada após `message/send` |
| `Api.Agente/A2A/ModelosA2A.cs` | `ParametrosRpcA2A` com `message` e `id` |
| `Agente.Application/Portas/Portas.cs` | Porta `IRepositorioTarefasA2A` |
| `Agente.Infrastructure/Memoria/RepositorioTarefasA2A.cs` | Novo: coleção `tarefas_a2a` no MongoDB, TTL `Memoria:TtlDias` |
| `tests/.../A2A/AutenticacaoSofiaTests.cs` | Novo: casos do critério de aceite |
| `specs/004-.../contracts/agent-card.schema.json` | `securitySchemes` e `security` obrigatórios |

A Task guardada contém o `ResultadoAnaliseSofia`, sem documentos nem nome do usuário (constitution 2.1.0, VII).

## Configuração

| Onde | Como |
|---|---|
| Local | `dotnet user-secrets set SOFIA_A2A_TOKEN "<CHAVE>" --project src/Agente/Api.Agente` (já feito nesta máquina) |
| Vercel | Variável `SOFIA_A2A_TOKEN` em Production, depois novo deploy |
| SOFIA | `CredentialRef` e `DiscoveryCredentialRef` com o **nome** `SOFIA_A2A_TOKEN` |

## Cadastro para a SOFIA (`A2A:Agents`)

Entregar ao professor. Domínio de produção: `agent-monitoramento-legislacao.vercel.app`.

```json
{
  "IdAgente": "agente-monitoramento-legislacao",
  "ExpectedName": "Agente de Monitoramento da Legislação e Jurisprudência",
  "Enabled": true,
  "CardUri": "https://agent-monitoramento-legislacao.vercel.app/.well-known/agent-card.json",
  "AllowedDestinations": [
    "https://agent-monitoramento-legislacao.vercel.app/.well-known/agent-card.json",
    "https://agent-monitoramento-legislacao.vercel.app/a2a"
  ],
  "AllowedAddresses": [
    "VALIDAR_COM_O_RESPONSAVEL_CENTRAL"
  ],
  "AllowedCapabilities": [
    "analisar_documentos",
    "monitorar_legislacao_tributaria"
  ],
  "CredentialRef": "SOFIA_A2A_TOKEN",
  "DiscoveryCredentialRef": "SOFIA_A2A_TOKEN"
}
```

`AllowedAddresses`: a Vercel serve por IPs anycast compartilhados e variáveis, sem IP fixo por projeto.
Não desativar a política da SOFIA: combinar com o responsável central como validar o destino (por DNS
ou pela faixa publicada pela Vercel).
