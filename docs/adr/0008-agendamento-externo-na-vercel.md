# ADR 0008 — Agendamento externo na Vercel

- **Status**: Aceito em 2026-11-09
- **Data**: 2026-11-05
- **Complementa**: ADR 0003, que continua valendo em hospedagens com processo contínuo
- **Referências**: `specs/002-publicacao-cloud` (D-02, FR-007, FR-008, FR-011)

## Contexto

A responsável pelo projeto decidiu manter a Api.Agente na Vercel. Lá a instância é encerrada após 5
minutos sem tráfego, então os `BackgroundService` do ADR 0003 não rodam na periodicidade esperada. Além
disso, cada subida a frio dispararia um ciclo de coleta. No plano Hobby, o cron da Vercel roda só uma vez
por dia.

## Decisão

- `Agendamento__Habilitado=false` na Vercel.
- Rotas `POST /api/v1/tarefas/coleta` e `POST /api/v1/tarefas/retencao`, protegidas por
  `Agendamento:TokenGatilho` e pelo mesmo bloqueio dos agendadores. Sem o token configurado, as rotas
  respondem 404.
- O GitHub Actions chama as rotas (`.github/workflows/agendamento-vercel.yml`).
- Uma coleta por dia, às 7h, e retenção às 2h, no horário de Brasília (decisão da responsável em
  2026-11-09). O chat só lê o que já foi coletado e não dispara coleta. Às 7h30 há uma segunda chamada,
  que só continua as fontes que o limite de 300 s tiver interrompido. Quando não há nada pendente, ela
  termina em menos de 1 s.
- Uma fonte que falhar às 7h só será tentada de novo no dia seguinte. A coleta sob demanda pelo chat foi
  descartada: a primeira consulta do dia esperaria o ciclo inteiro (de 21 s a 220 s sem o LLM), e sem
  login qualquer pessoa com a URL poderia disparar coletas.

## Alternativas

- Contêiner sempre ativo: não exige mudança no código, mas sai da Vercel.
- Vercel Pro com cron: tem custo, e também precisaria destas rotas.
- Daemon da Vercel: consome a cota de memória mesmo ocioso; ligado o mês todo, 1 GiB ultrapassa a cota do Hobby.

## Consequências

- O limite de 300 s do Hobby pode interromper o primeiro ciclo. Cada fonte é salva ao terminar, então a
  chamada das 7h30 continua nas fontes ainda devidas e na fila de classificação.
- Novidades publicadas depois das 7h só aparecem no chat no dia seguinte.
- Um ciclo interrompido pode deixar uma execução aberta em `execucoes_coleta`.
- O GitHub pode atrasar execuções agendadas, e o agendamento pode ser desativado em repositórios sem atividade.
- O token protege só estas rotas. Consultas e chat continuam sob o desvio do ADR 0007.
