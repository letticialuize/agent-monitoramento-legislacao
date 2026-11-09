# Tarefas: publicação da Api.Agente em nuvem

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [contracts/](contracts/ambiente-e-rotas.md)

Tipo: **A** (automatizável) ou **M** (manual, feita pela responsável no provedor). Nenhuma tarefa registra segredos.

## Fase 1: preparação

- [X] T201 [A] Criar `Dockerfile.vercel` (FR-001, FR-002)
- [X] T202 [A] Criar `.dockerignore` e `.env.example` e liberar o modelo no `.gitignore` (FR-005)
- [X] T203 [A] Construir a imagem e validar porta, disco somente leitura, rotas e `SIGTERM` (FR-001 a FR-004)
- [X] T204 [A] Registrar o resultado de restore, testes e publish no [plan.md](plan.md) (FR-004)

## Fase 2: decisões (bloqueia as fases seguintes)

- [X] T205 [M] Decidir o controle de acesso: publicar sem proteção por enquanto, com o ADR 0007 atualizado (D-01, FR-006)
- [X] T223 [M] Definir o prazo de revisão do desvio no ADR 0007: sem prazo por enquanto, por decisão da responsável em 2026-11-09 (D-01)
- [X] T206 [M] Decidir a hospedagem da coleta e da retenção: manter na Vercel (D-02, FR-007, FR-008)
- [X] T207 [M] Confirmar o plano da Vercel: Hobby, para teste sem uso comercial (D-03, FR-009)
- [X] T208 [M] Aprovar a regra de acesso de rede do Atlas: `0.0.0.0/0` com usuário restrito (D-04)
- [X] T209 [M] Definir o tratamento dos valores locais: as alterações do `appsettings.json` não entram no commit (D-05, FR-005)
- [X] T210 [A] Medir a duração de um ciclo real de coleta, sem LLM (D-02)
- [X] T222 [M] Aprovar o ADR 0008 e os horários do agendamento: coleta diária às 7h, com continuação às 7h30, e retenção às 2h (D-02)

## Fase 3: implementação decorrente das decisões

- [ ] T211 [A] Etapa posterior: implementar o controle de acesso (T118 da feature 001 ou outro mecanismo), com testes de 401 e 403 (FR-006)
- [X] T212 [A] Criar e testar as rotas protegidas de coleta e retenção e o workflow do GitHub Actions (FR-007, FR-008, FR-011)

## Fase 4: provisionamento

- [ ] T213 [M] Criar o projeto no Neon e obter a conexão direta com SSL ([quickstart.md](quickstart.md))
- [ ] T214 [M] Criar o cluster Atlas, o usuário restrito e a regra de rede
- [ ] T215 [M] Criar o projeto na Vercel e cadastrar as variáveis e os segredos do GitHub

## Fase 5: validação e operação

- [ ] T216 [M] Validar a URL pública e a recusa sem token nas rotas de tarefas (FR-004, FR-011)
- [ ] T217 [M] Confirmar migrações, catálogo e `unaccent` no Neon, e escrita, leitura e índice TTL no Atlas
- [ ] T218 [M] Confirmar execuções de coleta e de retenção no intervalo acordado (FR-007, FR-008)
- [ ] T219 [M] Medir subida a frio, duração e consumo em relação às cotas
- [ ] T220 [M] Revisar os logs e confirmar que não há segredos (FR-005)
- [ ] T221 [M] Executar e registrar o rollback (FR-010)
