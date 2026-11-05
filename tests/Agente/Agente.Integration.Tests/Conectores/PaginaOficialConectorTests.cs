using System.Text.Json;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Infrastructure.Conectores;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Agente.Integration.Tests.Conectores;

/// <summary>Recursos de configuração do conector PaginaOficial e falta de configuração nos conectores.</summary>
public sealed class PaginaOficialConectorTests : IDisposable
{
    private static readonly DateOnly Desde = new(2026, 1, 1);
    private static readonly DateOnly Ate = new(2026, 12, 31);

    private readonly WireMockServer _site = WireMockServer.Start();
    private readonly ServiceProvider _servicos;
    private readonly IHttpClientFactory _http;

    public PaginaOficialConectorTests()
    {
        var s = new ServiceCollection();
        s.AddHttpClient(Ajudas.ClienteHttp);
        _servicos = s.BuildServiceProvider();
        _http = _servicos.GetRequiredService<IHttpClientFactory>();
    }

    public void Dispose()
    {
        _servicos.Dispose();
        _site.Stop();
    }

    private void Pagina(string caminho, string html, int status = 200) =>
        _site.Given(Request.Create().WithPath(caminho).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "text/html; charset=utf-8").WithBody(html));

    private FonteParaColeta Fonte(object? cfg, TipoConector tipo = TipoConector.PaginaOficial) =>
        new(Guid.NewGuid(), "T-X", "X", _site.Url + "/", "Órgão X", NaturezaFonte.Oficial, tipo, cfg is null ? null : JsonSerializer.Serialize(cfg));

    private Task<ResultadoConector> Coletar(object? cfg) => new PaginaOficialConector(_http).ColetarAsync(Fonte(cfg), Desde, Ate, default);

    [Fact]
    public async Task Sem_seletores_e_falha_e_nao_coleta_vazia() =>
        await Assert.ThrowsAsync<ConfiguracaoFonteAusenteException>(() => Coletar(null));

    [Fact]
    public async Task Djen_e_querido_diario_sem_filtros_sao_falha()
    {
        await Assert.ThrowsAsync<ConfiguracaoFonteAusenteException>(() =>
            new DjenConector(_http).ColetarAsync(Fonte(new { tribunais = Array.Empty<string>() }, TipoConector.Djen), Desde, Ate, default));
        await Assert.ThrowsAsync<ConfiguracaoFonteAusenteException>(() =>
            new QueridoDiarioConector(_http).ColetarAsync(Fonte(new { territoryIds = Array.Empty<string>() }, TipoConector.QueridoDiario), Desde, Ate, default));
    }

    [Fact]
    public async Task Seletor_que_nao_encontra_nada_indica_pagina_alterada()
    {
        Pagina("/a", "<html><body><p>layout novo</p></body></html>");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coletar(new { urlListagem = $"{_site.Url}/a", seletorItem = "li.ato", seletorTitulo = ".t" }));
        Assert.Contains("página pode ter mudado", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pagina_sem_novidade_na_janela_nao_e_falha()
    {
        Pagina("/a", """<li class="ato"><a class="t" href="/x">Antigo 01/01/2020</a></li>""");
        var r = await Coletar(new { urlListagem = $"{_site.Url}/a", seletorItem = "li.ato", seletorTitulo = ".t" });
        Assert.Empty(r.Itens);
        Assert.False(r.Parcial);
    }

    [Fact]
    public async Task Varias_listagens_herdam_chaves_e_uma_falha_deixa_parcial()
    {
        Pagina("/ok", """<li class="ato"><a class="t" href="/1">Ato 10/03/2026</a></li>""");
        Pagina("/ruim", "erro", 500);
        var r = await Coletar(new
        {
            seletorItem = "li.ato",
            seletorTitulo = ".t",
            listagens = new object[] { new { urlListagem = $"{_site.Url}/ok", tipoAto = "Resolução" }, new { urlListagem = $"{_site.Url}/ruim" } },
        });
        var item = Assert.Single(r.Itens);
        Assert.Equal("Resolução", item.TipoAto);
        Assert.True(r.Parcial);
        Assert.Contains("/ruim", r.Aviso, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Todas_as_listagens_com_falha_e_falha_da_fonte()
    {
        Pagina("/ruim", "erro", 500);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coletar(new { urlListagem = $"{_site.Url}/ruim", seletorItem = "li", seletorTitulo = "a" }));
    }

    [Fact]
    public async Task Ano_na_url_data_no_detalhe_ementa_em_atributo_e_ultimos_itens()
    {
        Pagina("/atos/2026", """
            <ul>
              <li class="section-cv1"><a href="/atos/2026/cv1" title="Ementa um"><span>CONVÊNIO ICMS 1/26</span></a></li>
              <li class="section-cv2"><a href="/atos/2026/cv2" title="Ementa dois"><span>CONVÊNIO ICMS 2/26</span></a></li>
              <li class="navTreeItem"><a href="/outro"><span>Menu</span></a></li>
            </ul>
            """);
        Pagina("/atos/2026/cv2", """<p class="pub">Publicado no DOU de 29.01.26, pelo despacho</p>""");

        var r = await new PaginaOficialConector(_http).ColetarAsync(
            Fonte(new
            {
                urlListagem = $"{_site.Url}/atos/{{ano}}",
                seletorItem = "li[class*=\"section-cv\"]",
                seletorTitulo = "a span",
                atributoEmenta = "title",
                dataNoDetalhe = true,
                seletorData = ".pub",
                padraoData = @"DOU de (?<data>\d{2}\.\d{2}\.\d{2})",
                formatosData = new[] { "dd.MM.yy" },
                maxItens = 1,
                padraoNumero = @"(?<numero>\d+/\d+)",
                tipoAto = "Convênio ICMS",
            }),
            Desde,
            Ate,
            default);

        var item = Assert.Single(r.Itens);
        Assert.Equal("Ementa dois", item.Ementa);
        Assert.Equal("2/26", item.NumeroAto);
        Assert.Equal(new DateOnly(2026, 1, 29), item.DataPublicacao);
        Assert.EndsWith("/atos/2026/cv2", item.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Item_sem_link_usa_a_listagem_e_descricao_nao_repete_a_data()
    {
        Pagina("/avisos", """<table><tr><td>21/10/2026</td><td><b>Composição das Turmas</b></td></tr></table>""");
        Pagina("/noticias", """<div class="c"><h2><a href="/n1">Título</a></h2><span class="d"><span>28/10/2026</span> - Resumo</span></div>""");

        var avisos = await Coletar(new { urlListagem = $"{_site.Url}/avisos", seletorItem = "tr", seletorTitulo = "b", linkDaListagem = true, seletorData = "td:first-child" });
        Assert.EndsWith("/avisos", Assert.Single(avisos.Itens).Url, StringComparison.Ordinal);

        var noticias = await Coletar(new { urlListagem = $"{_site.Url}/noticias", seletorItem = "div.c", seletorTitulo = "h2", seletorData = ".d span", seletorDescricao = ".d" });
        Assert.Equal("Título — Resumo", Assert.Single(noticias.Itens).Ementa);
    }

    [Fact]
    public void Hosts_de_listagem_entram_na_lista_de_permissao()
    {
        var hosts = HandlerListaPermissao.HostsDaConfiguracao(JsonSerializer.Serialize(new
        {
            baseUrl = "https://api.exemplo.gov.br/v1",
            hostsPermitidos = new[] { "extra.gov.br" },
            urlListagem = "https://noticias.stf.jus.br/",
            listagens = new[] { new { urlListagem = "https://www.gov.br/x/{ano}" } },
        })).ToList();

        Assert.Equal(["api.exemplo.gov.br", "noticias.stf.jus.br", "www.gov.br", "extra.gov.br"], hosts);
    }
}
