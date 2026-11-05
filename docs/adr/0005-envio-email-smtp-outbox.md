# ADR 0005 — Envio do boletim por SMTP com padrão outbox

- **Status**: Aceito (implementação na Entrega 3)
- **Data**: 2026-10-29
- **Referências**: research.md (R-07); spec FR-019, FR-020

## Contexto

O boletim diário precisa ser enviado sem duplicidade por dia de referência e lista.

## Decisão

SMTP corporativo com MailKit atrás da porta `IEnvioEmail`. Boletim gravado com status `gerado`;
envio por destinatário registrado em `EnvioBoletim`; restrição única (`listaId`,
`diaReferenciaFim`); reenvio só para destinatários pendentes ou com falha.

## Alternativas

- SendGrid / Azure Communication Services: podem substituir a implementação da porta por novo ADR.

## Consequências

- Idempotência garantida pelo banco, sem dependência de provedor externo específico.