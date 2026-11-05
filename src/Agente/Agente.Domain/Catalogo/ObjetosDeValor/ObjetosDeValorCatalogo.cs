using System.Text.RegularExpressions;
using Agente.Domain.Comum;

namespace Agente.Domain.Catalogo.ObjetosDeValor;

/// <summary>Código estável de uma fonte no catálogo (ex.: FED-DOU).</summary>
public sealed partial record CodigoFonte : ObjetoDeValor
{
    public string Valor { get; }

    private CodigoFonte(string valor) => Valor = valor;

    public static Resultado<CodigoFonte> Criar(string? valor)
    {
        var v = valor?.Trim().ToUpperInvariant() ?? string.Empty;
        return FormatoValido().IsMatch(v)
            ? Resultado<CodigoFonte>.Ok(new CodigoFonte(v))
            : Resultado<CodigoFonte>.Falha("fonte.codigo_invalido", $"Código de fonte inválido: '{valor}'.");
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{1,63}$")]
    private static partial Regex FormatoValido();
}

/// <summary>URL absoluta de publicação; sabe dizer se pertence a domínio oficial.</summary>
public sealed record LinkOficial : ObjetoDeValor
{
    private static readonly string[] SufixosOficiais = [".gov.br", ".leg.br", ".jus.br"];

    public string Url { get; }

    public string Host { get; }

    private LinkOficial(Uri uri)
    {
        Url = uri.AbsoluteUri;
        Host = uri.Host.ToLowerInvariant();
    }

    public static Resultado<LinkOficial> Criar(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return Resultado<LinkOficial>.Falha("link.invalido", $"URL inválida: '{url}'.");
        }

        return Resultado<LinkOficial>.Ok(new LinkOficial(uri));
    }

    /// <summary>Verdadeiro quando o host termina em .gov.br, .leg.br ou .jus.br (ou é exatamente o domínio).</summary>
    public bool EhDominioOficial() =>
        SufixosOficiais.Any(s => Host.EndsWith(s, StringComparison.Ordinal) || Host == s[1..]);

    public override string ToString() => Url;
}

/// <summary>Ente federativo: UF e, opcionalmente, município.</summary>
public sealed partial record Ente : ObjetoDeValor
{
    public string Uf { get; }

    public string? Municipio { get; }

    private Ente(string uf, string? municipio)
    {
        Uf = uf;
        Municipio = municipio;
    }

    public static Resultado<Ente> Criar(string? uf, string? municipio = null)
    {
        var u = uf?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Uf2().IsMatch(u))
        {
            return Resultado<Ente>.Falha("ente.uf_invalida", $"UF inválida: '{uf}'.");
        }

        var m = string.IsNullOrWhiteSpace(municipio) ? null : municipio.Trim();
        return Resultado<Ente>.Ok(new Ente(u, m));
    }

    /// <summary>Converte "UF" ou "UF-Municipio".</summary>
    public static Resultado<Ente> Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Resultado<Ente>.Falha("ente.vazio", "Ente não informado.");
        }

        var partes = texto.Split('-', 2, StringSplitOptions.TrimEntries);
        return Criar(partes[0], partes.Length > 1 ? partes[1] : null);
    }

    public override string ToString() => Municipio is null ? Uf : $"{Uf}-{Municipio}";

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex Uf2();
}
