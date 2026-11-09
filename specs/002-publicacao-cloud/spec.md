# Especificação: publicação da Api.Agente em nuvem

**Feature**: `002-publicacao-cloud` | **Criada em**: 2026-11-05 | **Status**: rascunho, com decisões pendentes

## Objetivo

Publicar a Api.Agente existente em ambiente de nuvem, com Postgres e MongoDB gerenciados, sem alterar
as rotas e os contratos existentes nem o esquema do banco. Alvo inicial: Container Runtime da Vercel (beta), Neon e Atlas.

## Fora do escopo

- Criar contas, provisionar recursos ou publicar sem aprovação da responsável pelo projeto.
- Criar a Api.RAG, o pgvector ou o servidor A2A: nenhum deles existe nesta solução hoje.
- Migrar dados locais: a base é recriada pelas migrações e pela carga inicial do catálogo.

## Requisitos

| ID | Requisito | Critério de aceite |
|---|---|---|
| FR-001 | A imagem é construída a partir do repositório, sem segredos | Build de `Dockerfile.vercel` conclui; a imagem não contém `appsettings.Development.json` nem chaves |
| FR-002 | A API escuta na porta definida pela plataforma | Responde em `PORT` quando definida e na porta 80 quando não definida |
| FR-003 | A API funciona com sistema de arquivos somente leitura | `/saude` e `POST /api/v1/consultas` respondem com `--read-only --tmpfs /tmp` |
| FR-004 | Rotas e contratos são preservados | `/saude`, `POST /api/v1/consultas` e `POST /api/v1/conversas/{conversaId}/mensagens` seguem iguais; suítes de contrato passam |
| FR-005 | Segredos ficam fora do Git, da imagem e dos logs | Valores configurados só no provedor; `.env.example` sem valores reais |
| FR-006 | **Adiado (ver D-01).** A API publicada terá controle de acesso em etapa posterior | Requisição sem credencial é recusada na URL pública |
| FR-007 | A coleta roda na periodicidade esperada | Execuções registradas em `execucoes_coleta` no intervalo acordado, sem depender de tráfego de usuários (ver D-02) |
| FR-008 | A retenção de 30 dias é aplicada (constitution 4.3) | O expurgo roda ao menos uma vez por dia e o índice TTL do Mongo existe no Atlas (ver D-02) |
| FR-009 | O uso respeita os termos do plano contratado | Plano aprovado pela INFIS antes da publicação (ver D-03) |
| FR-010 | A publicação é reversível | Rollback executado e registrado ([quickstart.md](quickstart.md)) |
| FR-011 | Coleta e retenção podem ser disparadas por agendador externo, sem abrir a execução a terceiros | Com o token correto, executam; sem ele, respondem 401; sem token configurado, 404; com execução em andamento, 409. O token não aparece nos logs |

## Decisões pendentes

As decisões ainda abertas bloqueiam a publicação.

| ID | Decisão | Por que bloqueia |
|---|---|---|
| D-01 | **Resolvida em 2026-11-05: publicar sem proteção por enquanto e bloquear depois** (atualização do ADR 0007). Em 2026-11-09, a responsável decidiu não definir um prazo de revisão por enquanto | Não bloqueia mais. Riscos aceitos: quem tiver a URL consome a conta da OpenAI e lê conversas sabendo o `conversaId` |
| D-02 | **Resolvida em 2026-11-05: manter na Vercel**, com gatilho externo e rotas protegidas (ADR 0008, aceito em 2026-11-09): uma coleta por dia às 7h, com uma segunda chamada às 7h30 para continuar o que tiver sido interrompido, e retenção às 2h. O chat não dispara coleta | Os agendadores do ADR 0003 precisam de processo contínuo; na Vercel a instância é encerrada após 5 minutos sem tráfego |
| D-03 | **Resolvida em 2026-11-09: Vercel Hobby**, porque a publicação é um teste e não tem uso comercial | Não bloqueia mais. Antes de qualquer uso comercial, migrar para um plano pago |
| D-04 | **Resolvida em 2026-11-09: liberar `0.0.0.0/0` no Atlas**. Como compensação, o usuário do banco acessa só o banco `agente` e usa senha forte | Não bloqueia mais |
| D-05 | **Resolvida em 2026-11-05: as alterações locais do `appsettings.json` não entram no commit.** O arquivo continua no Git, com os segredos vazios | Não bloqueia mais. A publicação deve partir do Git, porque a CLI da Vercel envia os arquivos locais |
