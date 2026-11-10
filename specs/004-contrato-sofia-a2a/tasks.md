# Tarefas: contrato SOFIA v1.0 e Agent Card A2A

**Entrada**: [spec.md](spec.md), [plan.md](plan.md)

Tipo: **A** (automatizável) ou **M** (manual, feita pela responsável).

## Fase 1: decisões

- [X] T401 [M] Uso dos documentos da SOFIA: podem ir ao LLM como dado não confiável (ADR 0009)
- [X] T402 [M] Publicar o Agent Card já nesta etapa (ADR 0009)
- [X] T403 [M] Identidade: `agente-monitoramento-legislacao`

## Fase 2: contrato

- [X] T404 [A] DTOs e opções do contrato SOFIA v1.0 (FR-401, FR-404)
- [X] T405 [A] Validador de pedido e resposta: correlação, referências, limites, status × erro (FR-401 a FR-406, FR-409)
- [X] T406 [A] Schemas JSON e exemplos em `contracts/` (critério 1)

## Fase 3: A2A

- [X] T407 [A] Envelope A2A v0.3 e mapeador A2A ↔ contrato (FR-407)
- [X] T408 [A] Agent Card em `/.well-known/agent-card.json` com `A2A:UrlPublica` (FR-408)

## Fase 4: testes

- [X] T409 [A] Testes de contrato: exemplos, round-trip, campos ausentes, documento vazio, campo novo, limites, resultados inválidos, status erro
- [X] T410 [A] Testes A2A sem Docker: message/send, partes inválidas, round-trip Task, Task em andamento, Agent Card no schema
- [X] T411 [A] Rodar com Docker os testes da API completa (`AgentCardApiTests` e `CorsSofiaApiTests` da feature 003): aprovado em 2026-11-10

## Fase 5: pendências

- [X] T412 [A] Emenda 2.1.0 do Princípio VII redigida (a Seção 8.2 não admite desvio); aprovação formal em T512 da feature 005
- [ ] T413 [M] Definir `A2A__UrlPublica=https://SEU_AGENTE.vercel.app/a2a` na Vercel
- [X] T414 [A] Próxima etapa (skill `sofia-a2a-implementacao`): `POST /a2a`, `message/send`, caso de uso e testes ponta a ponta ([feature 005](../005-servidor-a2a-sofia/tasks.md))
- [ ] T415 [M] Confirmar no registry da SOFIA o `idAgente` e a URL do Agent Card

## Comandos

```bash
dotnet test tests/Agente/Agente.Contract.Tests --filter "FullyQualifiedName~ContratoSofia"
```

```bash
dotnet test tests/Agente/Agente.Integration.Tests --filter "FullyQualifiedName~ContratoSofia|FullyQualifiedName~AgentCard|FullyQualifiedName~CorsSofia"
```

```bash
curl -i https://SEU_AGENTE.vercel.app/.well-known/agent-card.json
```
