# ADR 0007 — Desvio temporário: Api.Agente sem autenticação

- **Status**: Aceito (desvio temporário, constitution Seção 8.2)
- **Data de aprovação**: 2026-11-05
- **Responsável e aprovador**: responsável do projeto
- **Prazo de revisão**: **a definir** pelo responsável; no máximo, antes de qualquer implantação fora
  da máquina de desenvolvimento
- **Suspende**: ADR 0004
- **Referências**: constitution v1.2.0 (Seção 5.1, Princípios XII e XIV); research.md (R-09)

## Regras afetadas

| Regra | O que exige | Situação durante o desvio |
|---|---|---|
| Seção 5.1 | Endpoints REST, MCP e A2A DEVEM exigir autenticação e autorização | Nenhum endpoint exige login ou papel |
| Princípio XII | Autorização decidida pelo sistema, com base no contexto do usuário | A verificação de papel das ferramentas continua no código, mas toda chamada usa o mesmo usuário fixo, que tem os papéis de consulta |
| Princípio XIV | Memória isolada por usuário e conversa | Isolada só por conversa: quem souber o `conversaId` lê e continua o histórico |

Nenhum dos princípios que não admitem desvio (IV, V, VII, X) é afetado.

## Justificativa

Decisão do responsável do projeto: o provedor de login ainda não foi escolhido (DP-13) e a prioridade
agora é usar a busca de novidades. A autenticação será implementada depois.

## Decisão

- Removidos o JWT Bearer, as políticas por papel e a configuração `Autenticacao`.
- Toda chamada usa o usuário fixo `sem-login`, com os papéis `consultor` e `desenvolvedor`
  (`ConsultasEndpoints`). O caso de uso e as ferramentas continuam recebendo `ContextoUsuario`.

## Impacto

- **A API não pode ser exposta**: quem alcançar a porta consulta novidades e, se a chave estiver
  configurada, consome a conta da OpenAI pelo chat. Usar só na máquina de desenvolvimento.
- `Consulta.UsuarioId` e a memória registram `sem-login`: a auditoria não identifica quem consultou.
- Endpoints futuros de revisão humana e de administração (T096, T110, T113 e seguintes) não devem
  ser usados fora do ambiente local enquanto este desvio valer: alteram dados sem identificar o autor.

## Como reverter

Tarefa T118: restaurar o que o ADR 0004 descreve (JWT Bearer, políticas `Consulta`, `Revisao` e
`Administracao`), trocar o usuário fixo pelo usuário do token e recolocar os testes de 401 e 403.
