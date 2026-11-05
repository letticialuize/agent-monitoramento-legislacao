using System.Globalization;
using System.Text.Json;
using Agente.Application.Portas;
using Agente.Domain.Comum;

namespace Agente.Infrastructure.Conectores;

// Formatos e endpoints conforme documentação oficial consultada em 2026-10-29
// (registrada em specs/001-monitoramento-boletim-diario/research.md, R-03).

/// <summary>
/// Senado Federal — Dados Abertos (https://legis.senado.leg.br/dadosabertos/docs/).
/// Lista: GET /legislacao/lista.json?data=AAAAMMDD (data de assinatura). Detalhe:
/// GET /legislacao/{id}.json, de onde vêm a data de publicação e o urlDocumento.
/// Sem autenticação; limite documentado de 10 req/s.
/// </summary>
internal sealed class SenadoConector(IHttpClientFactory http) : IConectorFonte
{
    public TipoConector Tipo => TipoConector.SenadoDadosAbertos;

    public async Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var cfg = ConfiguracaoConector.Ler(fonte.Configuracao);
        var baseUrl = (cfg.Texto("baseUrl") ?? "https://legis.senado.leg.br/dadosabertos").TrimEnd('/');
        var max = cfg.Inteiro("maxNormasPorDia", 50);
        var cliente = http.CreateClient(Ajudas.ClienteHttp);
        var itens = new List<ItemColetado>();
        var semPublicacao = 0;

        foreach (var dia in Ajudas.Dias(desde, ate))
        {
            using var lista = await Ajudas.GetJsonAsync(cliente, $"{baseUrl}/legislacao/lista.json?data={dia:yyyyMMdd}", ct);
            foreach (var norma in Normas(lista.RootElement).Take(max))
            {
                var id = Ajudas.Str(norma, "id");
                var ementa = Ajudas.Str(norma, "ementa");
                if (id is null || string.IsNullOrWhiteSpace(ementa))
                {
                    continue;
                }

                using var detalhe = await Ajudas.GetJsonAsync(cliente, $"{baseUrl}/legislacao/{Uri.EscapeDataString(id)}.json", ct);
                var publicacao = DataPublicacao(detalhe.RootElement);
                if (publicacao is null)
                {
                    semPublicacao++;
                }

                itens.Add(new ItemColetado(
                    publicacao ?? dia,
                    "Senado Federal",
                    Ajudas.Str(norma, "descricao") ?? Ajudas.Str(norma, "tipo") ?? "Norma",
                    Ajudas.Str(norma, "numero"),
                    ementa,
                    UrlDocumento(detalhe.RootElement) ?? $"{baseUrl}/legislacao/{id}"));
            }
        }

        var aviso = semPublicacao > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{semPublicacao} norma(s) sem data de publicação no detalhe; usada a data de assinatura.")
            : null;
        return new ResultadoConector(itens, semPublicacao > 0, aviso);
    }

    /// <summary>Procura o primeiro array de objetos com "ementa" (estrutura de lista do serviço).</summary>
    private static IEnumerable<JsonElement> Normas(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
        {
            if (e.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("ementa", out _)))
            {
                return e.EnumerateArray().ToList();
            }

            return e.EnumerateArray().SelectMany(Normas).ToList();
        }

        return e.ValueKind == JsonValueKind.Object
            ? e.EnumerateObject().SelectMany(p => Normas(p.Value)).ToList()
            : [];
    }

    private static DateOnly? DataPublicacao(JsonElement raiz)
    {
        foreach (var pub in Buscar(raiz, "publicacao"))
        {
            var lista = pub.ValueKind == JsonValueKind.Array ? pub.EnumerateArray() : Enumerable.Repeat(pub, 1);
            foreach (var p in lista)
            {
                var d = Ajudas.Data(Ajudas.Str(p, "data"), "dd/MM/yyyy", "yyyy-MM-dd");
                if (d is not null)
                {
                    return d;
                }
            }
        }

        return null;
    }

    private static string? UrlDocumento(JsonElement raiz) =>
        Buscar(raiz, "urlDocumento").Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static IEnumerable<JsonElement> Buscar(JsonElement e, string nome)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (p.NameEquals(nome))
                {
                    yield return p.Value;
                }

                foreach (var x in Buscar(p.Value, nome))
                {
                    yield return x;
                }
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in e.EnumerateArray())
            {
                foreach (var x in Buscar(i, nome))
                {
                    yield return x;
                }
            }
        }
    }
}

/// <summary>
/// Câmara dos Deputados — API v2 (https://dadosabertos.camara.leg.br/swagger/api.html).
/// GET /proposicoes?dataApresentacaoInicio&amp;dataApresentacaoFim&amp;siglaTipo&amp;pagina&amp;itens (máx. 100).
/// Sem autenticação.
/// </summary>
internal sealed class CamaraConector(IHttpClientFactory http) : IConectorFonte
{
    public TipoConector Tipo => TipoConector.CamaraDadosAbertos;

    public async Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var cfg = ConfiguracaoConector.Ler(fonte.Configuracao);
        var baseUrl = (cfg.Texto("baseUrl") ?? "https://dadosabertos.camara.leg.br/api/v2").TrimEnd('/');
        var itensPorPagina = Math.Clamp(cfg.Inteiro("itensPorPagina", 100), 1, 100);
        var maxPaginas = Math.Max(1, cfg.Inteiro("maxPaginas", 5));
        var siglas = cfg.Lista("siglasTipo");
        var cliente = http.CreateClient(Ajudas.ClienteHttp);
        var itens = new List<ItemColetado>();
        var truncado = false;

        foreach (var sigla in siglas.Count > 0 ? siglas.Cast<string?>() : [null])
        {
            for (var pagina = 1; pagina <= maxPaginas; pagina++)
            {
                var url = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{baseUrl}/proposicoes?dataApresentacaoInicio={desde:yyyy-MM-dd}&dataApresentacaoFim={ate:yyyy-MM-dd}&pagina={pagina}&itens={itensPorPagina}&ordem=ASC&ordenarPor=id");
                if (sigla is not null)
                {
                    url += $"&siglaTipo={Uri.EscapeDataString(sigla)}";
                }

                using var doc = await Ajudas.GetJsonAsync(cliente, url, ct);
                var dados = doc.RootElement.TryGetProperty("dados", out var d) && d.ValueKind == JsonValueKind.Array ? d : default;
                if (dados.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                foreach (var p in dados.EnumerateArray())
                {
                    var data = Ajudas.Data(Ajudas.Str(p, "dataApresentacao"), "yyyy-MM-dd");
                    var ementa = Ajudas.Str(p, "ementa");
                    if (data is null || string.IsNullOrWhiteSpace(ementa))
                    {
                        continue;
                    }

                    var numero = Ajudas.Str(p, "numero");
                    var ano = Ajudas.Str(p, "ano");
                    itens.Add(new ItemColetado(
                        data.Value,
                        "Câmara dos Deputados",
                        Ajudas.Str(p, "siglaTipo") ?? "Proposição",
                        numero is null ? null : (ano is null ? numero : $"{numero}/{ano}"),
                        ementa,
                        Ajudas.Str(p, "uri") ?? $"{baseUrl}/proposicoes/{Ajudas.Str(p, "id")}"));
                }

                var temProxima = doc.RootElement.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array &&
                    links.EnumerateArray().Any(l => Ajudas.Str(l, "rel") == "next");
                if (!temProxima)
                {
                    break;
                }

                if (pagina == maxPaginas)
                {
                    truncado = true;
                }
            }
        }

        return new ResultadoConector(itens, truncado, truncado ? $"Coleta limitada a {maxPaginas} página(s) por tipo." : null);
    }
}

/// <summary>
/// DJEN — API de comunicações do CNJ (https://comunicaapi.pje.jus.br/swagger/djen.yml).
/// GET /comunicacao?siglaTribunal&amp;dataDisponibilizacaoInicio&amp;dataDisponibilizacaoFim&amp;pagina&amp;itensPorPagina.
/// A API exige siglaTribunal (ou filtro textual) para páginas com mais de 5 itens; por isso a
/// configuração da fonte DEVE informar os tribunais. Sem autenticação para consulta.
/// </summary>
internal sealed class DjenConector(IHttpClientFactory http) : IConectorFonte
{
    public TipoConector Tipo => TipoConector.Djen;

    public async Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var cfg = ConfiguracaoConector.Ler(fonte.Configuracao);
        var tribunais = cfg.Lista("tribunais");
        if (tribunais.Count == 0)
        {
            throw new ConfiguracaoFonteAusenteException(fonte.Codigo, "tribunais; filtro obrigatório pela API do DJEN");
        }

        var baseUrl = (cfg.Texto("baseUrl") ?? "https://comunicaapi.pje.jus.br/api/v1").TrimEnd('/');
        var texto = cfg.Texto("texto");
        var itensPorPagina = Math.Clamp(cfg.Inteiro("itensPorPagina", 100), 5, 100);
        var maxPaginas = Math.Max(1, cfg.Inteiro("maxPaginas", 5));
        var cliente = http.CreateClient(Ajudas.ClienteHttp);
        var itens = new List<ItemColetado>();
        var truncado = false;

        foreach (var tribunal in tribunais)
        {
            for (var pagina = 1; pagina <= maxPaginas; pagina++)
            {
                var url = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{baseUrl}/comunicacao?siglaTribunal={Uri.EscapeDataString(tribunal)}&dataDisponibilizacaoInicio={desde:yyyy-MM-dd}&dataDisponibilizacaoFim={ate:yyyy-MM-dd}&pagina={pagina}&itensPorPagina={itensPorPagina}");
                if (texto is not null)
                {
                    url += $"&texto={Uri.EscapeDataString(texto)}";
                }

                using var doc = await Ajudas.GetJsonAsync(cliente, url, ct);
                if (!doc.RootElement.TryGetProperty("items", out var lista) || lista.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                var quantidade = 0;
                foreach (var c in lista.EnumerateArray())
                {
                    quantidade++;
                    var data = Ajudas.Data(Ajudas.Str(c, "data_disponibilizacao"), "yyyy-MM-dd")
                        ?? Ajudas.Data(Ajudas.Str(c, "datadisponibilizacao"), "dd/MM/yyyy");
                    var conteudo = Ajudas.SemHtml(Ajudas.Str(c, "texto"), 1000);
                    if (data is null || conteudo.Length == 0)
                    {
                        continue;
                    }

                    var sigla = Ajudas.Str(c, "siglaTribunal") ?? tribunal;
                    var orgao = Ajudas.Str(c, "nomeOrgao");
                    itens.Add(new ItemColetado(
                        data.Value,
                        orgao is null ? sigla : $"{sigla} - {orgao}",
                        Ajudas.Str(c, "tipoDocumento") ?? Ajudas.Str(c, "tipoComunicacao") ?? "Comunicação",
                        Ajudas.Str(c, "numeroComunicacao") ?? Ajudas.Str(c, "numeroprocessocommascara") ?? Ajudas.Str(c, "numero_processo"),
                        conteudo,
                        Ajudas.Str(c, "link") ?? $"{baseUrl}/comunicacao?numeroComunicacao={Ajudas.Str(c, "numeroComunicacao")}&siglaTribunal={sigla}"));
                }

                if (quantidade < itensPorPagina)
                {
                    break;
                }

                if (pagina == maxPaginas)
                {
                    truncado = true;
                }
            }
        }

        return new ResultadoConector(itens, truncado, truncado ? $"Coleta limitada a {maxPaginas} página(s) por tribunal." : null);
    }
}

/// <summary>
/// Querido Diário — API pública (https://docs.queridodiario.ok.org.br/pt-br/latest/utilizando/api-publica.html).
/// GET /gazettes?territory_ids&amp;published_since&amp;published_until&amp;querystring&amp;size&amp;offset.
/// Granularidade: diário inteiro. Fonte sempre de natureza agregador_apoio (Princípio II).
/// </summary>
internal sealed class QueridoDiarioConector(IHttpClientFactory http) : IConectorFonte
{
    public TipoConector Tipo => TipoConector.QueridoDiario;

    public async Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var cfg = ConfiguracaoConector.Ler(fonte.Configuracao);
        var territorios = cfg.Lista("territoryIds");
        if (territorios.Count == 0)
        {
            throw new ConfiguracaoFonteAusenteException(fonte.Codigo, "territoryIds; códigos IBGE");
        }

        var baseUrl = (cfg.Texto("baseUrl") ?? "https://api.queridodiario.org.br").TrimEnd('/');
        var tamanho = Math.Clamp(cfg.Inteiro("size", 50), 1, 100);
        var consulta = cfg.Texto("querystring");
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl}/gazettes?published_since={desde:yyyy-MM-dd}&published_until={ate:yyyy-MM-dd}&size={tamanho}&offset=0&sort_by=descending_date&excerpt_size=500&number_of_excerpts=2");
        url = territorios.Aggregate(url, (u, t) => u + $"&territory_ids={Uri.EscapeDataString(t)}");
        if (consulta is not null)
        {
            url += $"&querystring={Uri.EscapeDataString(consulta)}";
        }

        var cliente = http.CreateClient(Ajudas.ClienteHttp);
        using var doc = await Ajudas.GetJsonAsync(cliente, url, ct);
        var itens = new List<ItemColetado>();
        if (doc.RootElement.TryGetProperty("gazettes", out var diarios) && diarios.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in diarios.EnumerateArray())
            {
                var data = Ajudas.Data(Ajudas.Str(g, "date"), "yyyy-MM-dd");
                var link = Ajudas.Str(g, "url");
                if (data is null || link is null)
                {
                    continue;
                }

                var municipio = $"{Ajudas.Str(g, "territory_name")}/{Ajudas.Str(g, "state_code")}";
                var edicao = Ajudas.Str(g, "edition");
                var extra = g.TryGetProperty("is_extra_edition", out var e) && e.ValueKind == JsonValueKind.True;
                var trechos = g.TryGetProperty("excerpts", out var ex) && ex.ValueKind == JsonValueKind.Array
                    ? string.Join(" … ", ex.EnumerateArray().Select(x => Ajudas.SemHtml(x.GetString(), 500)))
                    : string.Empty;

                itens.Add(new ItemColetado(
                    data.Value,
                    $"Município de {municipio}",
                    extra ? "Diário Oficial Municipal (edição extra)" : "Diário Oficial Municipal",
                    edicao,
                    trechos.Length > 0 ? trechos : $"Diário oficial de {municipio}, edição {edicao ?? "s/n"}.",
                    link));
            }
        }

        var total = doc.RootElement.TryGetProperty("total_gazettes", out var t) && t.TryGetInt32(out var n) ? n : itens.Count;
        return new ResultadoConector(itens, total > itens.Count, total > itens.Count ? $"{total - itens.Count} diário(s) além do limite de {tamanho}." : null);
    }
}
