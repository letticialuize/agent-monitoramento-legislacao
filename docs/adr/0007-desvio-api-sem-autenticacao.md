# ADR 0007 — Desvio temporário: Api.Agente sem autenticação

- **Status**: Aceito (desvio temporário, constitution Seção 8.2)
- **Data de aprovação**: 2026-11-05
- **Responsável e aprovador**: responsável do projeto
- **Prazo de revisão**: nenhum por enquanto, por decisão da responsável em 2026-11-09. A constitution (Seção 8.2) exige um prazo, então essa pendência continua aberta
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

- Quem alcançar a API consulta novidades e, se a chave estiver configurada, consome a conta da OpenAI
  pelo chat. A exposição só é aceita nas condições da atualização abaixo.
- `Consulta.UsuarioId` e a memória registram `sem-login`: a auditoria não identifica quem consultou.
- Endpoints futuros de revisão humana e de administração (T096, T110, T113 e seguintes) não devem
  ser usados fora do ambiente local enquanto este desvio valer: alteram dados sem identificar o autor.

## Atualização de 2026-11-05: publicação sem proteção

Decisão da responsável do projeto: publicar na Vercel, por enquanto, sem controle de acesso em
`/api/v1/consultas` e `/api/v1/conversas`. O bloqueio será implementado depois.

- **Riscos aceitos:** qualquer pessoa com a URL pode consultar novidades, gastar a cota da OpenAI e da
  Vercel e, sabendo o `conversaId`, ler e continuar uma conversa.
- **Cuidados nesta fase:** chave da OpenAI exclusiva deste ambiente e com limite de gasto; URL não
  divulgada; nenhum dado sensível ou de cliente no chat.
- **Fora do desvio:** as rotas `/api/v1/tarefas` continuam exigindo token (ADR 0008).
- **Prazo de revisão:** em 2026-11-09, a responsável decidiu não definir um por enquanto. A constitution
  (Seção 8.2) exige o prazo, então ele deve ser definido antes de qualquer uso além do teste.

## Atualização de 2026-11-09: Swagger no ambiente publicado

Decisão da responsável do projeto: manter o Swagger (`/swagger`) e o OpenAPI (`/openapi/v1.json`)
ligados na Vercel, para a integração com o ambiente publicado.

- **Como:** `Swagger:Habilitado=true`, definido no `Dockerfile.vercel`. Fora de Development, o padrão
  continua desligado.
- **Risco aceito:** quem tiver a URL vê as rotas e os contratos e pode testar as chamadas pela tela. As
  rotas `/api/v1/tarefas` ficam fora do OpenAPI.
- **Como desligar:** definir `Swagger__Habilitado=false` nas variáveis do projeto na Vercel e publicar de
  novo.

## Como reverter

Tarefa T118: restaurar o que o ADR 0004 descreve (JWT Bearer, políticas `Consulta`, `Revisao` e
`Administracao`), trocar o usuário fixo pelo usuário do token e recolocar os testes de 401 e 403.
