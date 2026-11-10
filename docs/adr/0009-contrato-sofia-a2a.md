# ADR 0009 — Contrato SOFIA v1.0 sobre A2A v0.3 e documentos recebidos da SOFIA

- **Status**: Aceito
- **Data**: 2026-11-10
- **Responsável e aprovador**: responsável do projeto
- **Referências**: constitution (Princípios VII, X e XI; Seções 3.3, 4.1 e 5.1; DP-18); ADR 0007; feature 004

## Contexto

A SOFIA (orquestrador da INFIS) chama os agentes especializados pelo protocolo A2A, com um contrato de
negócio comum à turma: `SolicitacaoAnaliseSofia` na ida e `ResultadoAnaliseSofia` na volta. O pedido traz
o nome do usuário, a pergunta e documentos já convertidos em texto.

## Decisões

1. **Protocolo (resolve DP-18):** A2A v0.3.x, transporte JSON-RPC, método `message/send` síncrono, sem
   streaming nem push. O contrato SOFIA v1.0 viaja como JSON em `TextPart`; a resposta vai no Artifact
   `analise-sofia-v1` de uma Task `completed`. Versão do contrato (`1.0`) e do protocolo (`0.3.0`) são
   independentes.
2. **Identidade:** `idAgente = agente-monitoramento-legislacao`, configurável em `Sofia:IdAgente`.
3. **Agent Card:** publicado em `GET /.well-known/agent-card.json`, sem autenticação (descoberta
   pública, sem segredos). O `url` aponta para `POST /a2a`, implementado na feature 005.
4. **Documentos da SOFIA:** decisão da responsável: o agente pode receber e enviar ao LLM o texto dos
   documentos, porque produz notícias de legislação e não trata documentos de cliente. Condições:
   - o texto é dado não confiável (Princípio X): delimitado como dado no prompt, nunca como instrução;
   - o texto e o nome do usuário não são persistidos nem registrados em log;
   - limites de tamanho configurados em `Sofia:*` recusam o pedido, sem truncar.

## Conflito com a constitution

O Princípio VII diz que o agente só recebe nome e nicho do cliente. A decisão 4 aceita outro conteúdo
vindo da SOFIA. A Seção 8.2 proíbe desvio do Princípio VII, então o caminho é emenda (Seção 8.3).

**Atualização de 2026-11-10:** emenda 2.1.0 da constitution redigida com a feature 005. O Princípio VII
passa a ter a regra das solicitações da SOFIA (contexto no LLM como dado não confiável, sem persistir nem
logar documento e nome do usuário), e DP-18 foi resolvida. Emenda aprovada pela responsável em 2026-11-10 (T512).

## Consequências

- O mesmo contrato e o mesmo validador valem para o servidor A2A da próxima etapa (Seção 3.3).
- A autenticação continua fora do contrato e segue o ADR 0007 (sem login). A Seção 5.1 continua sem
  atendimento também no A2A.
- Mudança incompatível no contrato exige `versaoContrato` nova e testes de compatibilidade.
