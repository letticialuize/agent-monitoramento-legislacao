# Plano: publicação da Api.Agente em nuvem

**Spec**: [spec.md](spec.md) | **Contrato**: [ambiente-e-rotas.md](contracts/ambiente-e-rotas.md) | **Roteiro**: [quickstart.md](quickstart.md)

## Solução inspecionada

- **Host:** `src/Agente/Api.Agente/Api.Agente.csproj`, que gera `Api.Agente.dll`, em .NET 10.
- **Bancos:** Postgres (EF Core, com migrações na subida e a extensão `unaccent`) e MongoDB (memória do chat).
- **Processos em segundo plano:** `AgendadorColeta` e `AgendadorRetencao`.
- **Ausentes nesta solução:** A2A, Agent Card, pgvector, CORS, autenticação e limite de requisições.

## Matriz de compatibilidade

Consulta às páginas oficiais em 2026-11-05.

| Item | Situação | Detalhe |
|---|---|---|
| Imagem .NET 10 no Container Runtime | OK | A Vercel aceita `Dockerfile.vercel`, ainda em beta. Sem teste na plataforma |
| Porta | OK | A Vercel usa a porta 80 por padrão, salvo `PORT` no projeto. Por isso o padrão difere do modelo da skill, que usava 8080 |
| Disco somente leitura | OK | Testado localmente com `--read-only --tmpfs /tmp` |
| Fuso `America/Sao_Paulo` | OK | Dados de fuso presentes na imagem |
| Encerramento por `SIGTERM` | OK | Parada limpa em cerca de 1,4 s; a Vercel concede 30 s |
| `unaccent` no Neon | OK | Listada como suportada pelo Neon. Sem teste no Neon |
| pgvector | Não se aplica | A Api.Agente usa busca textual (ADR 0002) |
| A2A e Agent Card | Não se aplica | Integração futura; não há contrato a preservar |
| Health check | OK | O caminho é `/saude`, não `/health` |
| Migrações e carga na subida | Adaptação | Cada subida a frio refaz as verificações: cerca de 4,7 s com o banco pronto e 7 s com banco vazio, em rede local. Medir com Neon e Atlas |
| Bloqueio consultivo | Adaptação | Exige a conexão direta do Neon, não a conexão com pooling |
| Rede até o Atlas | Adaptação | D-04: `0.0.0.0/0` liberado, com usuário restrito ao banco `agente` e senha forte |
| Agendadores | Adaptação | Desligados na Vercel e substituídos por rotas protegidas chamadas pelo GitHub Actions (ADR 0008). Sem teste na plataforma |
| Exposição sem autenticação | Risco aceito | Publicação sem proteção por enquanto (D-01, ADR 0007). Só as rotas de tarefas exigem token |
| Plano Hobby para a INFIS | Risco aceito | D-03: Hobby aceito para teste, sem uso comercial |

## Agendadores na Vercel

- A instância é encerrada após 5 minutos sem tráfego, então o `PeriodicTimer` não dispara sozinho no intervalo de 30 minutos.
- `AgendadorColeta` executa um ciclo logo ao subir. Com tráfego esporádico, cada subida a frio dispara uma
  coleta, que consome CPU e pode ser interrompida quando a instância é encerrada.
- `AgendadorRetencao` só avalia o horário depois de 5 minutos de instância ativa, então o expurgo pode nunca rodar.
- O cron do Hobby roda só uma vez por dia, com precisão de até 59 minutos, e chama HTTP. Não há endpoint de coleta hoje.
- O Daemon é pensado para laços contínuos, e o tempo ocioso consome a cota. Com 1 GiB ligado o mês todo, são
  cerca de 720 GB-h contra a cota de 420 GB-h do Hobby.

**Decisão (2026-11-05):** manter na Vercel, com gatilho externo ([ADR 0008](../../docs/adr/0008-agendamento-externo-na-vercel.md)).

- `Agendamento__Habilitado=false` evita que cada subida a frio dispare uma coleta.
- O GitHub Actions chama `POST /api/v1/tarefas/coleta` uma vez por dia, às 7h, com uma segunda chamada às 7h30 só para continuar o que o limite de 300 s tiver interrompido. `POST /api/v1/tarefas/retencao` roda às 2h. Todos os horários são de Brasília (decisão de 2026-11-09).
- O chat não dispara coleta. Fontes com falha só são tentadas de novo no dia seguinte.
- **Cota (estimativa):** cerca de 90 chamadas por mês. Com cada uma mantendo a instância ativa por 5 minutos e 2 GB de memória, seriam cerca de 15 GB-h de 360 GB-h. Confirmar a memória real e o consumo no painel.
- **Duração:** dois primeiros ciclos reais, sem o LLM, levaram cerca de 220 s e 21 s. Com o LLM, o tempo aumenta. Se passar de 300 s, a chamada seguinte continua o trabalho pendente.

## Limites dos planos gratuitos

Valores de 2026-11-05, que podem mudar. Custos da OpenAI, domínios e serviços de terceiros não estão incluídos.

| Serviço | Limites principais | Ao atingir o limite |
|---|---|---|
| Vercel Hobby | Uso pessoal e não comercial; 300 s por execução; 4 h de CPU ativa e 360 GB-h de memória por mês; logs de 1 hora | Pode ser preciso aguardar 30 dias para voltar a usar o recurso |
| Atlas Free | 0,5 GB; 100 operações/s; 500 conexões; 10 GB de entrada e 10 GB de saída a cada 7 dias | Limitação de velocidade; pausa após 30 dias sem conexões |
| Neon Free | 1 GB por projeto; 100 CU-h e 5 GB de saída por mês; suspende após 5 minutos ociosos | Compute suspenso ou escrita bloqueada; os dados não são apagados |

## Conformidade com a constitution

| Regra | Situação |
|---|---|
| Seção 5.1 (autenticação) | Desvio temporário aceito no ADR 0007. Falta o prazo de revisão exigido pela Seção 8.2 |
| Seção 5.1 (segredos) | A constitution permite segredos em `appsettings*.json`; a skill de DevOps proíbe em arquivos versionados. Este plano segue a regra mais restritiva: só variáveis no provedor |
| Seção 4.3 (retenção de 30 dias) | Atendida pela rota de retenção diária, depende de o agendador externo rodar |
| Princípio XIV (memória) | Sem autenticação, o isolamento continua sendo só por conversa (ADR 0007) |
| Princípio XVII (simplicidade) | Nenhum projeto ou pacote novo |
| ADR 0003 (agendador) | Complementado pelo ADR 0008, ainda proposto. Os agendadores internos continuam disponíveis em outras hospedagens |

## Mudanças propostas

- **Novos:** `Dockerfile.vercel`, `.dockerignore`, `.env.example`, `.github/workflows/agendamento-vercel.yml`, `TarefasEndpoints.cs`, `TarefasTests.cs`, ADR 0008 e esta pasta de especificação.
- **Alterados:** `.gitignore`, para versionar `.env.example`; `Program.cs`, para mapear as rotas; `Agendadores.cs`, para compartilhar os nomes dos bloqueios.
- **Sem alteração:** rotas e contratos existentes, migrações e Docker Compose local.

## Evidências locais (2026-11-05)

Imagem `Dockerfile.vercel`, com 389 MB, usando Postgres 17 e Mongo 8 temporários e `Agendamento__Habilitado=false`.

| Verificação | Resultado |
|---|---|
| `dotnet restore`, `dotnet test` e `dotnet publish` (no build da imagem) | Concluídos; 184 testes aprovados, 1 ignorado e nenhuma falha, rodados em cópia do projeto porque a API local bloqueava as DLLs |
| Rotas de tarefas na imagem, sem credencial | HTTP 401 |
| `POST /api/v1/tarefas/retencao` com token | HTTP 200 |
| `POST /api/v1/tarefas/coleta` com token, fontes reais e sem LLM | Primeira chamada: HTTP 200 em 21 s, 26 fontes, 185 novidades e 10 falhas. Segunda no mesmo dia: 0,2 s, com novas tentativas só nas 10 fontes com falha |
| Ciclo real pelo agendador interno, sem LLM | Cerca de 220 s, 26 fontes, 182 novidades e 11 falhas |
| Token nos logs do contêiner | Nenhuma ocorrência |
| `GET /saude` com `PORT=8080` e sem `PORT` | HTTP 200 nas duas portas (8080 e 80) |
| `POST /api/v1/consultas` | HTTP 200, `concluido_com_limitacoes`, pois ainda não houve coleta |
| Chat sem chave da OpenAI | HTTP 200, `falha_tecnica`, com aviso de LLM não configurado |
| Swagger em produção | HTTP 404 sem `Swagger:Habilitado`. Desde 2026-11-09, a Vercel liga a opção (ADR 0007) |
| Primeira subida | Migrações aplicadas e `unaccent` criada. Um log de erro esperado: o EF consulta `__EFMigrationsHistory` antes de a tabela existir |
| Sem variáveis, só com o `appsettings.json` local | Falha ao conectar em `127.0.0.1:55432` e o contêiner encerra com código 139 |

**Não verificado:** deploy na Vercel, Neon, Atlas, TLS, GitHub Actions, chat com a OpenAI real e a duração de um ciclo com o LLM.
