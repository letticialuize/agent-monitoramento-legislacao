using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using UglyToad.PdfPig;

namespace Agente.Infrastructure.Conectores;

/// <summary>
/// Conector genérico para páginas de órgãos sem API (research R-03). Nada é presumido sobre a
/// página: a extração depende da configuração aprovada no catálogo. A chave "listagens" é opcional
/// e permite várias páginas por fonte; cada listagem herda as chaves que não redefinir.
/// <code>
/// {
///   "listagens": [ { "urlListagem": "...", "tipoAto": "..." } ],
///   "urlListagem": "https://.../{ano}",        // opcional; padrão: link oficial. {ano} = ano(s) da janela
///   "seletorItem": "css",                      // obrigatório
///   "seletorTitulo": "css",                    // obrigatório (itens sem título são ignorados)
///   "seletorLink": "css",                      // opcional (padrão: o próprio item ou seu primeiro &lt;a&gt;)
///   "atributoEmenta": "title",                 // opcional: atributo do link com a ementa
///   "seletorDescricao": "css",                 // opcional: acrescentado à ementa
///   "ementaTextoDoItem": true,                 // opcional: ementa = texto inteiro do item
///   "linkDaListagem": true,                    // opcional: item sem link aponta para a listagem
///   "seletorData": "css",                      // opcional (padrão: texto do item)
///   "formatosData": ["dd/MM/yyyy"],            // opcional
///   "padraoData": "regex com grupo 'data'",    // opcional
///   "dataNoDetalhe": true,                     // opcional: data lida da página do item
///   "maxItens": 15,                            // opcional: só os N últimos itens da página
///   "padraoNumero": "regex com grupo 'numero'",// opcional (aplicado ao texto do item)
///   "padraoTipo": "regex com grupo 'tipo'",    // opcional (aplicado ao texto do item)
///   "tipoAto": "texto padrão",                 // opcional
///   "extrairTrechoPdf": true                   // opcional: baixa o PDF do item e extrai o início
/// }
/// </code>
/// Falta de configuração e seletor que não encontra nenhum item viram falha técnica: a fonte não
/// pode parecer consultada sem ter sido (Princípio IV).
/// </summary>
internal sealed partial class PaginaOficialConector(IHttpClientFactory http) : IConectorFonte
{
    private const int TamanhoMaximoEmenta = 2000;
    private static readonly string[] FormatosPadrao = ["dd/MM/yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];

    public TipoConector Tipo => TipoConector.PaginaOficial;

    public async Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var listagens = ConfiguracaoConector.Ler(fonte.Configuracao).Listagens();
        if (listagens.Any(l => l.Texto("seletorItem") is null || l.Texto("seletorTitulo") is null))
        {
            throw new ConfiguracaoFonteAusenteException(fonte.Codigo, "seletorItem/seletorTitulo");
        }

        var cliente = http.CreateClient(Ajudas.ClienteHttp);
        var itens = new List<ItemColetado>();
        var falhas = new List<string>();
        var semData = 0;
        var paginas = 0;

        foreach (var cfg in listagens)
        {
            var urlConfigurada = cfg.Texto("urlListagem") ?? fonte.Url;
            foreach (var url in Urls(urlConfigurada, desde, ate))
            {
                paginas++;
                try
                {
                    semData += await ColetarPaginaAsync(cliente, fonte, cfg, url, desde, ate, itens, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or SeletorSemItensException)
                {
                    falhas.Add($"{url}: {ex.Message}");
                }
            }
        }

        if (falhas.Count == paginas)
        {
            throw new InvalidOperationException(string.Join(" | ", falhas));
        }

        var avisos = new List<string>();
        if (falhas.Count > 0)
        {
            avisos.Add($"Listagem(ns) com falha: {string.Join(" | ", falhas)}");
        }

        if (semData > 0)
        {
            avisos.Add($"{semData} item(ns) sem data reconhecível foram ignorados.");
        }

        return new ResultadoConector(itens, avisos.Count > 0, avisos.Count > 0 ? string.Join(" ", avisos) : null);
    }

    private static IEnumerable<string> Urls(string url, DateOnly desde, DateOnly ate)
    {
        if (!url.Contains("{ano}", StringComparison.Ordinal))
        {
            return [url];
        }

        return Enumerable.Range(desde.Year, ate.Year - desde.Year + 1)
            .Select(a => url.Replace("{ano}", a.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    private static async Task<int> ColetarPaginaAsync(
        HttpClient cliente, FonteParaColeta fonte, ConfiguracaoConector cfg, string url, DateOnly desde, DateOnly ate, List<ItemColetado> itens, CancellationToken ct)
    {
        var baseUri = new Uri(url);
        var html = await Ajudas.GetTextoAsync(cliente, baseUri, ct);
        var documento = await new HtmlParser().ParseDocumentAsync(html, ct);

        var seletorTitulo = cfg.Texto("seletorTitulo")!;
        var candidatos = documento.QuerySelectorAll(cfg.Texto("seletorItem")!)
            .Where(i => i.QuerySelector(seletorTitulo) is not null)
            .ToList();
        if (candidatos.Count == 0)
        {
            throw new SeletorSemItensException();
        }

        var maximo = cfg.Inteiro("maxItens", 0);
        if (maximo > 0)
        {
            candidatos = candidatos.TakeLast(maximo).ToList();
        }

        var formatos = cfg.Lista("formatosData") is { Count: > 0 } f ? f.ToArray() : FormatosPadrao;
        var padraoData = Regex_(cfg.Texto("padraoData"), RegexOptions.None);
        var padraoNumero = Regex_(cfg.Texto("padraoNumero"), RegexOptions.IgnoreCase);
        var padraoTipo = Regex_(cfg.Texto("padraoTipo"), RegexOptions.IgnoreCase);
        var tipoPadrao = cfg.Texto("tipoAto") ?? "Ato";
        var semData = 0;

        foreach (var item in candidatos)
        {
            var titulo = Limpar(item.QuerySelector(seletorTitulo)?.TextContent);
            if (titulo.Length == 0)
            {
                continue;
            }

            var ancora = Ancora(item, cfg.Texto("seletorLink"));
            Uri? link = null;
            if (ancora?.GetAttribute("href") is { } href && Uri.TryCreate(baseUri, href, out var absoluto))
            {
                link = absoluto;
            }
            else if (cfg.Booleano("linkDaListagem"))
            {
                link = baseUri;
            }

            if (link is null)
            {
                continue;
            }

            var textoData = cfg.Booleano("dataNoDetalhe")
                ? await TextoDetalheAsync(cliente, link, cfg.Texto("seletorData"), ct)
                : cfg.Texto("seletorData") is { } sd ? item.QuerySelector(sd)?.TextContent : item.TextContent;
            var data = LerData(textoData, padraoData, formatos);
            if (data is null)
            {
                semData++;
                continue;
            }

            if (data < desde || data > ate)
            {
                continue;
            }

            var textoItem = Limpar(item.TextContent);
            itens.Add(new ItemColetado(
                data.Value,
                fonte.OrgaoEmissor,
                Grupo(padraoTipo, textoItem, "tipo") ?? tipoPadrao,
                Grupo(padraoNumero, textoItem, "numero"),
                Ementa(item, ancora, titulo, textoItem, cfg),
                link.AbsoluteUri,
                cfg.Booleano("extrairTrechoPdf") && link.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? await TrechoPdfAsync(cliente, link, ct)
                    : null));
        }

        return semData;
    }

    private static IElement? Ancora(IElement item, string? seletorLink)
    {
        if (seletorLink is not null)
        {
            return item.QuerySelector(seletorLink);
        }

        return item.Matches("a[href]") ? item : item.QuerySelector("a[href]");
    }

    private static string Ementa(IElement item, IElement? ancora, string titulo, string textoItem, ConfiguracaoConector cfg)
    {
        string ementa;
        if (cfg.Booleano("ementaTextoDoItem"))
        {
            ementa = textoItem;
        }
        else if (cfg.Texto("atributoEmenta") is { } atributo && Limpar(ancora?.GetAttribute(atributo)) is { Length: > 0 } valor)
        {
            ementa = valor;
        }
        else
        {
            var descricao = cfg.Texto("seletorDescricao") is { } sd ? SemDataInicial(Limpar(item.QuerySelector(sd)?.TextContent)) : string.Empty;
            ementa = descricao.Length > 0 ? $"{titulo} — {descricao}" : titulo;
        }

        return ementa.Length > TamanhoMaximoEmenta ? ementa[..TamanhoMaximoEmenta] + "…" : ementa;
    }

    /// <summary>Texto da página do item, para fontes cuja listagem não traz a data (ex.: DOU em atos do CONFAZ).</summary>
    private static async Task<string?> TextoDetalheAsync(HttpClient cliente, Uri link, string? seletorData, CancellationToken ct)
    {
        try
        {
            var html = await Ajudas.GetTextoAsync(cliente, link, ct);
            var documento = await new HtmlParser().ParseDocumentAsync(html, ct);
            return seletorData is null ? documento.Body?.TextContent : documento.QuerySelector(seletorData)?.TextContent;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? string.Empty : Espacos().Replace(texto, " ").Trim();

    private static string SemDataInicial(string texto) => DataInicial().Replace(texto, string.Empty).Trim();

    private static Regex? Regex_(string? padrao, RegexOptions opcoes) =>
        padrao is null ? null : new Regex(padrao, opcoes | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static DateOnly? LerData(string? texto, Regex? padrao, string[] formatos)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        if (padrao is not null)
        {
            return padrao.Match(texto) is { Success: true } m && m.Groups["data"].Success
                ? Ajudas.Data(m.Groups["data"].Value, formatos)
                : null;
        }

        return Ajudas.Data(DataSolta().Match(texto) is { Success: true } d ? d.Value : texto, formatos);
    }

    private static string? Grupo(Regex? padrao, string texto, string nome) =>
        padrao?.Match(texto) is { Success: true } m && m.Groups[nome].Success ? m.Groups[nome].Value.Trim() : null;

    private static async Task<string?> TrechoPdfAsync(HttpClient cliente, Uri link, CancellationToken ct)
    {
        var bytes = await cliente.GetByteArrayAsync(link, ct);
        using var pdf = PdfDocument.Open(bytes);
        var texto = string.Join(" ", pdf.GetPages().Take(2).Select(p => p.Text));
        return texto.Length > 1500 ? texto[..1500] : texto;
    }

    [GeneratedRegex(@"\b\d{2}[/.-]\d{2}[/.-]\d{4}\b|\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DataSolta();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}\s*-?\s*")]
    private static partial Regex DataInicial();

    private sealed class SeletorSemItensException()
        : Exception("Os seletores não encontraram nenhum item; a página pode ter mudado.");
}
