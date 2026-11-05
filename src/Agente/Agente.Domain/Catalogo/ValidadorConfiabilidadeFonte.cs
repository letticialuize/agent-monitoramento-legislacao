using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Catalogo;

/// <summary>
/// Critérios de confiabilidade da constitution (Princípio II, FR-030): uma fonte só é "oficial"
/// quando usa domínio .gov.br, .leg.br ou .jus.br e tem órgão emissor. Exceção de domínio exige
/// aprovador registrado.
/// </summary>
public sealed class ValidadorConfiabilidadeFonte
{
    public Resultado Validar(NaturezaFonte natureza, LinkOficial link, string? orgaoEmissor, string? excecaoAprovadaPor)
    {
        if (string.IsNullOrWhiteSpace(orgaoEmissor))
        {
            return Resultado.Falha("fonte.orgao_obrigatorio", "Órgão emissor é obrigatório.");
        }

        if (natureza == NaturezaFonte.AgregadorApoio)
        {
            return Resultado.Ok();
        }

        if (link.EhDominioOficial())
        {
            return Resultado.Ok();
        }

        return string.IsNullOrWhiteSpace(excecaoAprovadaPor)
            ? Resultado.Falha(
                "fonte.dominio_nao_oficial",
                $"Fonte oficial exige domínio .gov.br, .leg.br ou .jus.br (host '{link.Host}'); registre-a como agregador_apoio ou com exceção aprovada.")
            : Resultado.Ok();
    }
}
