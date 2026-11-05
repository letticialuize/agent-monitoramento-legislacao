using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agente.Domain.Catalogo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Infrastructure.Conectores;

/// <summary>Destino fora da lista de permissão: tratado como falha técnica (Seção 5.1).</summary>
public sealed class DestinoNaoPermitidoException(string host)
    : InvalidOperationException($"Destino '{host}' não está na lista de permissão derivada do catálogo vigente.");

/// <summary>Fonte sem a configuração obrigatória: não coletada, e isso aparece como falha técnica (Princípio IV).</summary>
public sealed class ConfiguracaoFonteAusenteException(string fonte, string chaves)
    : InvalidOperationException($"Fonte {fonte} sem configuração obrigatória ({chaves}); nada foi coletado.");

/// <summary>
/// Só permite requisições a hosts das fontes ativas do catálogo vigente (link oficial, baseUrl,
/// urlListagem e hostsPermitidos da configuração do conector), e seus subdomínios. Vale também para
/// o destino final de redirecionamentos.
/// </summary>
internal sealed partial class HandlerListaPermissao(IServiceScopeFactory escopos, IMemoryCache cache) : DelegatingHandler
{
    private const string ChaveCache = "conectores.hosts-permitidos";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = request.RequestUri?.Host.ToLowerInvariant() ?? string.Empty;
        var permitidos = await HostsAsync(cancellationToken);
        if (!Permitido(host, permitidos))
        {
            throw new DestinoNaoPermitidoException(host);
        }

        var resposta = await base.SendAsync(request, cancellationToken);
        var hostFinal = resposta.RequestMessage?.RequestUri?.Host.ToLowerInvariant();
        if (hostFinal is not null && !Permitido(hostFinal, permitidos))
        {
            resposta.Dispose();
            throw new DestinoNaoPermitidoException(hostFinal);
        }

        return resposta;
    }

    internal static bool Permitido(string host, IReadOnlySet<string> permitidos) =>
        permitidos.Any(p => host == p || host.EndsWith("." + p, StringComparison.Ordinal));

    private async Task<IReadOnlySet<string>> HostsAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ChaveCache, out IReadOnlySet<string>? hosts) && hosts is not null)
        {
            return hosts;
        }

        using var escopo = escopos.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IRepositorioVersoesCatalogo>();
        var vigente = await repositorio.ObterVigenteAsync(ct);
        var conjunto = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in vigente?.FontesAtivas() ?? [])
        {
            conjunto.Add(f.LinkOficial.Host);
            foreach (var h in HostsDaConfiguracao(f.ConfiguracaoConector))
            {
                conjunto.Add(h);
            }
        }

        cache.Set(ChaveCache, (IReadOnlySet<string>)conjunto, TimeSpan.FromMinutes(5));
        return conjunto;
    }

    private static IEnumerable<string> UrlsDeListagem(JsonElement raiz)
    {
        var elementos = new List<JsonElement> { raiz };
        if (raiz.TryGetProperty("listagens", out var listagens) && listagens.ValueKind == JsonValueKind.Array)
        {
            elementos.AddRange(listagens.EnumerateArray());
        }

        foreach (var e in elementos)
        {
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("urlListagem", out var u) && Uri.TryCreate(u.GetString(), UriKind.Absolute, out var uri))
            {
                yield return uri.Host.ToLowerInvariant();
            }
        }
    }

    internal static IEnumerable<string> HostsDaConfiguracao(string? configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao))
        {
            yield break;
        }

        using var doc = JsonDocument.Parse(configuracao);
        if (doc.RootElement.TryGetProperty("baseUrl", out var b) && Uri.TryCreate(b.GetString(), UriKind.Absolute, out var u))
        {
            yield return u.Host.ToLowerInvariant();
        }

        foreach (var url in UrlsDeListagem(doc.RootElement))
        {
            yield return url;
        }

        if (doc.RootElement.TryGetProperty("hostsPermitidos", out var lista) && lista.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in lista.EnumerateArray())
            {
                var s = h.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    yield return s.Trim().ToLowerInvariant();
                }
            }
        }
    }
}
/// <summary>Leitura tolerante da configuração JSON do conector (vinda do catálogo aprovado).</summary>
internal sealed class ConfiguracaoConector
{
    private readonly JsonElement _raiz;
    private readonly ConfiguracaoConector? _pai;

    private ConfiguracaoConector(JsonElement raiz, ConfiguracaoConector? pai = null)
    {
        _raiz = raiz;
        _pai = pai;
    }

    /// <summary>Uma configuração por listagem, herdando as chaves da raiz; sem "listagens", a própria raiz.</summary>
    public IReadOnlyList<ConfiguracaoConector> Listagens() =>
        _raiz.ValueKind == JsonValueKind.Object && _raiz.TryGetProperty("listagens", out var l) && l.ValueKind == JsonValueKind.Array && l.GetArrayLength() > 0
            ? l.EnumerateArray().Select(e => new ConfiguracaoConector(e, this)).ToList()
            : [this];

    public static ConfiguracaoConector Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConfiguracaoConector(default);
        }

        using var doc = JsonDocument.Parse(json);
        return new ConfiguracaoConector(doc.RootElement.Clone());
    }

    public string? Texto(string nome) =>
        _raiz.ValueKind == JsonValueKind.Object && _raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString())
            : _pai?.Texto(nome);

    public int Inteiro(string nome, int padrao) =>
        _raiz.ValueKind == JsonValueKind.Object && _raiz.TryGetProperty(nome, out var v) && v.TryGetInt32(out var i) ? i : _pai?.Inteiro(nome, padrao) ?? padrao;

    public bool Booleano(string nome) =>
        _raiz.ValueKind == JsonValueKind.Object && _raiz.TryGetProperty(nome, out var v)
            ? v.ValueKind == JsonValueKind.True
            : _pai?.Booleano(nome) ?? false;

    public IReadOnlyList<string> Lista(string nome) =>
        _raiz.ValueKind == JsonValueKind.Object && _raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(e => e.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
            : _pai?.Lista(nome) ?? [];
}

internal static partial class Ajudas
{
    public const string ClienteHttp = "conectores";

    public static string SemHtml(string? html, int maximo)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var texto = WebUtility.HtmlDecode(Tags().Replace(html, " "));
        texto = Espacos().Replace(texto, " ").Trim();
        return texto.Length > maximo ? texto[..maximo] + "…" : texto;
    }

    public static DateOnly? Data(string? texto, params string[] formatos)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var t = texto.Trim();
        foreach (var f in formatos)
        {
            if (DateTime.TryParseExact(t.Length > f.Length ? t[..f.Length] : t, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                return DateOnly.FromDateTime(d);
            }
        }

        return null;
    }

    public static async Task<JsonDocument> GetJsonAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        requisicao.Headers.Accept.ParseAdd("application/json");
        using var resposta = await http.SendAsync(requisicao, ct);
        resposta.EnsureSuccessStatusCode();
        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public static async Task<string> GetTextoAsync(HttpClient http, Uri url, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,*/*;q=0.8");
        using var resposta = await http.SendAsync(requisicao, ct);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadAsStringAsync(ct);
    }

    public static string? Str(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind != JsonValueKind.Null
            ? v.ToString()
            : null;

    public static IEnumerable<DateOnly> Dias(DateOnly desde, DateOnly ate)
    {
        for (var d = desde; d <= ate; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();
}
