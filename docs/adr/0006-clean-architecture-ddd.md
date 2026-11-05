# ADR 0006 — Clean Architecture e DDD na Api.Agente

- **Status**: Aceito
- **Data**: 2026-10-29
- **Referências**: plan.md ("Arquitetura: Clean Architecture e DDD"); research.md (R-01);
  constitution (Princípios III, XIII, XVII)

## Contexto

As regras determinísticas (Princípio III) precisam ser testáveis sem infraestrutura, e a
constitution exige separação entre endpoints, aplicação, domínio e infraestrutura, com
complexidade justificada.

## Decisão

- Quatro projetos: `Api.Agente` (apresentação e composition root), `Agente.Application` (casos de
  uso e portas), `Agente.Domain` (modelo puro) e `Agente.Infrastructure` (adaptadores).
- DDD tático por contexto em pastas: Catálogo, Monitoramento, Clientes, Boletim, Operação.
  Agregados sem setters públicos, objetos de valor, serviços de domínio, um repositório por raiz.
- Casos de uso como classes simples chamadas por DI (sem MediatR); sem eventos de domínio na v1.
- **Exceções de dependência aprovadas para a Application**: `JsonSchema.Net` (validação dos
  contratos JSON da saída do LLM) e as abstrações `Microsoft.Extensions.Logging.Abstractions`,
  `Microsoft.Extensions.Options` e `Microsoft.Extensions.DependencyInjection.Abstractions`.

## Alternativas

- Projeto único com pastas: não garante camadas em compilação.
- Um projeto por contexto: multiplica projetos sem ganho no volume atual.
- MediatR e eventos de domínio: indireção sem fluxo que precise deles.

## Consequências

- Testes de arquitetura (`tests/Agente/Agente.Domain.Tests/ArquiteturaTests.cs`) verificam a regra
  de dependência e a ausência de setters públicos em agregados.
- O EF Core mapeia os agregados por configuração fluente, sem atributos no domínio.