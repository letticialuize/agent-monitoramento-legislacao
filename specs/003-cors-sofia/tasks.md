# Tarefas: CORS para o frontend da SOFIA

**Entrada**: [spec.md](spec.md), [plan.md](plan.md)

Tipo: **A** (automatizável) ou **M** (manual, feita pela responsável).

## Fase 1: decisões

- [X] T301 [M] Informar as origens do frontend: `http://localhost:4200` e `https://sofia-infis.onrender.com`
- [X] T302 [M] Decidir a autenticação da SOFIA: manter sem autenticação (ADR 0007)

## Fase 2: configuração

- [X] T303 [A] Criar a política `SofiaBrowser` com validação das origens (FR-305, FR-306, FR-307)
- [X] T304 [A] Aplicar a política só ao grupo de consultas e conversas e ligar `UseCors` (FR-301, FR-304)
- [X] T305 [A] Configurar `Cors:AllowedOrigins` por ambiente e documentar a variável (FR-305)

## Fase 3: testes

- [X] T306 [A] Testes da política isolada, sem Docker: origem válida e inválida, preflight permitido e negado, lista vazia, rota fora da política
- [X] T307 [A] Testes na API completa: preflight nas rotas reais, origem negada, `/api/v1/tarefas` e `/saude` fora, chamada sem `Origin`, `Vary: Origin` e `X-Correlation-Id` exposto
- [X] T308 [A] Rodar T307 com o Docker ligado (Testcontainers): aprovado em 2026-11-10

## Fase 4: verificação ponta a ponta

- [ ] T309 [M] Na Vercel, conferir com curl o preflight das duas origens e de uma origem desconhecida ([testes](#comandos))
- [ ] T310 [M] Pelo frontend da SOFIA em produção, chamar `POST /api/v1/consultas` e ver a resposta no navegador, sem erro de CORS no console

## Comandos

```bash
curl -i -X OPTIONS https://SEU_AGENTE.vercel.app/api/v1/consultas -H "Origin: https://sofia-infis.onrender.com" -H "Access-Control-Request-Method: POST" -H "Access-Control-Request-Headers: content-type"
```

```bash
curl -i -X OPTIONS https://SEU_AGENTE.vercel.app/api/v1/consultas -H "Origin: https://site-nao-autorizado.example" -H "Access-Control-Request-Method: POST"
```

```bash
curl -i -X POST https://SEU_AGENTE.vercel.app/api/v1/consultas -H "Content-Type: application/json" --data "{}"
```

Esperado: 204 com `Access-Control-Allow-Origin: https://sofia-infis.onrender.com`; sem esse cabeçalho
para a origem desconhecida; 200 sem cabeçalhos CORS na chamada sem `Origin`.
