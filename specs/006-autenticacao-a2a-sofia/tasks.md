# Tarefas: autenticação A2A e cadastro na SOFIA

**Entrada**: [spec.md](spec.md), [plan.md](plan.md)

Tipo: **A** (automatizável) ou **M** (manual, feita pela responsável ou pelo professor).

## Implementação e testes

- [X] T601 [A] Filtro Bearer no card e no RPC, com 401, 503 e comparação em tempo constante (FR-601 a FR-604)
- [X] T602 [A] Agent Card com `securitySchemes`, `security`, `text/plain` e `analisar_documentos` (FR-605)
- [X] T603 [A] `tasks/get` e `tasks/cancel` com Task guardada no MongoDB (FR-606, FR-607)
- [X] T604 [A] Testes de autenticação, configuração ausente, card, tarefas, erros e logs
- [X] T605 [A] `SOFIA_A2A_TOKEN` no user-secrets local

## Deploy e cadastro

- [ ] T606 [M] Criar `SOFIA_A2A_TOKEN` na Vercel (Production) e fazer novo deploy
- [ ] T607 [M] Conferir na URL pública: card sem chave = 401; com chave = 200; `message/send` = Task
- [ ] T608 [M] Entregar ao professor o objeto de cadastro do [plan.md](plan.md), com o domínio real
- [ ] T609 [M] Professor: validar `AllowedAddresses` (Vercel sem IP fixo) e cadastrar em `A2A:Agents`
- [ ] T610 [M] Teste ponta a ponta pela SOFIA com documento sintético

## Comandos

Substituir `<CHAVE>`; não registrar a chave em arquivos versionados.

```bash
curl -i https://agent-monitoramento-legislacao.vercel.app/.well-known/agent-card.json
```

```bash
curl -i https://agent-monitoramento-legislacao.vercel.app/.well-known/agent-card.json -H "Authorization: Bearer <CHAVE>"
```

```bash
curl -s -X POST https://agent-monitoramento-legislacao.vercel.app/a2a -H "Authorization: Bearer <CHAVE>" -H "Content-Type: application/json" --data @specs/004-contrato-sofia-a2a/contracts/exemplos/a2a-message-send.json
```

```bash
dotnet test tests/Agente/Agente.Integration.Tests --filter "FullyQualifiedName~A2A"
```
