# ADR 0003 — Agendador interno à Api.Agente

- **Status**: Aceito
- **Data**: 2026-10-29
- **Referências**: research.md (R-02); constitution (Princípio XVII)

## Contexto

Coleta (por periodicidade da fonte), boletim (dias úteis, 8h) e expurgo de retenção precisam rodar
de forma agendada, uma única vez mesmo com várias instâncias.

## Decisão

`BackgroundService` dentro da Api.Agente, com relógio injetável (`IRelogio`) no fuso
`America/Sao_Paulo` e bloqueio consultivo do PostgreSQL (`pg_try_advisory_lock`) exposto pela porta
`IBloqueioExecucao`. Agendadores apenas chamam casos de uso.

## Alternativas

- Quartz.NET ou Hangfire: mais recursos, mais dependências e tabelas próprias.
- Serviço worker separado: novo implantável sem necessidade atual.

## Consequências

- Sem nova dependência; adoção de biblioteca de agendamento futura exige novo ADR.