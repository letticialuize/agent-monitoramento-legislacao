# Roteiro de publicação

Passos manuais, a executar só depois de resolver as decisões pendentes da [spec.md](spec.md). Limites
e telas mudam: confira as páginas oficiais no dia do deploy.

## 1. Neon

1. Crie um projeto e escolha a região mais próxima da aplicação.
2. Em **Connect**, desative **Connection pooling** e copie os dados da conexão direta.
3. Monte `ConnectionStrings__Postgres` no formato do [.env.example](../../.env.example), com `SSL Mode=Require`.

## 2. MongoDB Atlas

1. Crie um projeto e um cluster **Free** na região mais próxima da aplicação.
2. Em **Database Access**, crie um usuário com leitura e escrita só no banco `agente`.
3. Em **Network Access**, aplique a decisão D-04.
4. Em **Connect > Drivers**, copie a URI `mongodb+srv://` e escape os caracteres especiais da senha.

## 3. Vercel

1. Importe o repositório. O **Root Directory** é a raiz, onde está o `Dockerfile.vercel`.
2. Confirme o preset **Container**.
3. Em **Settings > Environment Variables**, cadastre as variáveis obrigatórias do [contrato](contracts/ambiente-e-rotas.md).
4. Em **Settings > Functions**, escolha a região mais próxima dos bancos.
5. Use em `Llm__ApiKey` uma chave da OpenAI só para este ambiente, com limite de gasto no painel da OpenAI.
6. Publique. Não divulgue a URL: consultas e chat ficam sem login nesta fase (ADR 0007).

Publique pelo Git. A CLI (`vercel --prod`) envia os arquivos locais, incluindo o `appsettings.json` alterado,
então só a use a partir de uma cópia limpa do repositório.

## 4. Agendamento no GitHub

1. Gere um token aleatório e longo. Cadastre esse valor como `Agendamento__TokenGatilho` na Vercel.
2. No repositório, abra **Settings > Secrets and variables > Actions**.
3. Crie o segredo `AGENTE_TOKEN_GATILHO`, com o mesmo token.
4. Crie a variável `AGENTE_API_URL`, com a URL pública, sem barra no final.
5. Em **Actions > Agendamento Vercel > Run workflow**, rode `coleta` e depois `retencao` para validar.

O agendamento só vale depois que o workflow estiver na branch padrão. Se o primeiro ciclo passar de 300 s,
o job falha com tempo esgotado e a chamada seguinte continua as fontes pendentes.

## 5. Validação na URL pública

Nesta fase, as rotas abaixo respondem sem credencial. Já as rotas de tarefas devem responder 401 sem o token.

```bash
curl -i https://SUA_URL/saude
```

```bash
curl -i -X POST https://SUA_URL/api/v1/consultas -H "Content-Type: application/json" -d '{"assunto":"CBS"}'
```

```bash
curl -i -X POST https://SUA_URL/api/v1/conversas/teste-publicacao/mensagens -H "Content-Type: application/json" -d '{"texto":"Quais as novidades sobre CBS?"}'
```

Depois, confira:

- nos logs da Vercel, a subida sem erros além dos esperados e nenhum segredo;
- no Neon, as tabelas criadas e a versão vigente do catálogo;
- no Atlas, a coleção `conversas`, com a mensagem de teste e o índice `ttl_atualizado_em`;
- em `execucoes_coleta`, as execuções no intervalo acordado em D-02.

## 6. Rollback

- **Aplicação:** em **Deployments**, promova a implantação anterior com **Promote to Production** ou
  **Instant Rollback**.
- **Bancos:** faça um snapshot do Neon antes de publicar uma versão com nova migração. O plano Free
  tem janela de restauração curta, então confirme o limite atual.
- **Segredos:** se algum vazar, gere outro no provedor, atualize a variável e publique de novo.
- **Docker Compose local:** continua igual e não faz parte do rollback.
