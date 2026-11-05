using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agente.Domain.Comum;

namespace Agente.Domain.Monitoramento;

/// <summary>
/// Identidade natural de um ato para deduplicação entre fontes (research R-05):
/// "órgão emissor + tipo + número + ano" normalizada quando há número; senão hash da ementa
/// normalizada + órgão + data de publicação.
/// </summary>
public sealed record ChaveDeduplicacao : ObjetoDeValor
{
    public string Valor { get; }

    private ChaveDeduplicacao(string valor) => Valor = valor;

    public static ChaveDeduplicacao Criar(string orgaoEmissor, string tipoAto, string? numeroAto, DateOnly dataPublicacao, string ementa)
    {
        var orgao = Texto.Normalizar(orgaoEmissor);
        var tipo = Texto.Normalizar(tipoAto);
        var numero = NormalizarNumero(numeroAto);

        if (numero.Length > 0)
        {
            return new ChaveDeduplicacao(
                string.Create(CultureInfo.InvariantCulture, $"n|{orgao}|{tipo}|{numero}|{dataPublicacao.Year}"));
        }

        var baseHash = string.Create(CultureInfo.InvariantCulture, $"{Texto.Normalizar(ementa)}|{orgao}|{dataPublicacao:yyyy-MM-dd}");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(baseHash)));
        return new ChaveDeduplicacao($"h|{hash}");
    }

    /// <summary>Reconstrói a partir do valor persistido.</summary>
    public static ChaveDeduplicacao DeValor(string valor) => new(valor);

    /// <summary>Remove separadores e zeros à esquerda: "Nº 2.250" → "2250".</summary>
    private static string NormalizarNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return string.Empty;
        }

        var simples = new string(Texto.Normalizar(numero).Where(char.IsAsciiLetterOrDigit).ToArray());
        foreach (var prefixo in new[] { "numero", "no", "n" })
        {
            if (simples.StartsWith(prefixo, StringComparison.Ordinal) && simples.Length > prefixo.Length && char.IsAsciiDigit(simples[prefixo.Length]))
            {
                simples = simples[prefixo.Length..];
                break;
            }
        }

        return simples.TrimStart('0');
    }

    public override string ToString() => Valor;
}
