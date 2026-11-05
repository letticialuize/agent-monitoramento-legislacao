using Agente.Domain.Catalogo;
using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Tests.Catalogo;

internal static class Dados
{
    public static readonly DateTimeOffset Agora = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public static DadosFonte Fonte(
        string codigo = "FED-DOU",
        string link = "https://www.in.gov.br/",
        NaturezaFonte natureza = NaturezaFonte.Oficial,
        EsferaFonte esfera = EsferaFonte.Federal,
        string? ente = null,
        string orgao = "Imprensa Nacional",
        string? excecao = null) =>
        new(codigo, "Fonte", link, "Atos", orgao, esfera, ente, Grupo.LegislacaoFederal, natureza,
            [PapelResponsavel.AJ], [Formato.Html], TipoConector.PaginaOficial, null, Periodicidade.Diaria, excecao);

    public static VersaoCatalogo Versao() => VersaoCatalogo.Criar("2026-10-29", "docs/fontes/x.pdf", "aprovador", Agora).Valor;
}

public class ObjetosDeValorCatalogoTests
{
    [Theory]
    [InlineData("https://www.in.gov.br/", true)]
    [InlineData("https://legis.senado.leg.br/x", true)]
    [InlineData("https://comunica.pje.jus.br/", true)]
    [InlineData("https://gov.br/x", true)]
    [InlineData("https://gov.br.exemplo.com/", false)]
    [InlineData("https://www.ioerj.com.br/", false)]
    [InlineData("https://queridodiario.ok.org.br/", false)]
    public void Reconhece_dominio_oficial(string url, bool oficial) =>
        Assert.Equal(oficial, LinkOficial.Criar(url).Valor.EhDominioOficial());

    [Theory]
    [InlineData("ftp://x.gov.br")]
    [InlineData("nao-e-url")]
    [InlineData("")]
    public void Link_invalido_falha(string url) => Assert.False(LinkOficial.Criar(url).Sucesso);

    [Fact]
    public void Ente_aceita_uf_e_municipio()
    {
        var e = Ente.Parse("SP-São Paulo").Valor;
        Assert.Equal("SP", e.Uf);
        Assert.Equal("São Paulo", e.Municipio);
        Assert.False(Ente.Criar("XYZ").Sucesso);
    }

    [Theory]
    [InlineData("fed-dou", true)]
    [InlineData("A", false)]
    [InlineData("com espaço", false)]
    public void Codigo_de_fonte(string codigo, bool valido) => Assert.Equal(valido, CodigoFonte.Criar(codigo).Sucesso);
}

public class VersaoCatalogoTests
{
    private readonly ValidadorConfiabilidadeFonte _validador = new();

    [Fact]
    public void Aceita_fonte_oficial_valida()
    {
        var v = Dados.Versao();
        Assert.True(v.AdicionarFonte(Dados.Fonte(), _validador, Dados.Agora).Sucesso);
        Assert.Single(v.Fontes);
        Assert.True(v.Fontes[0].Ativa);
    }

    [Fact]
    public void Recusa_codigo_duplicado()
    {
        var v = Dados.Versao();
        v.AdicionarFonte(Dados.Fonte(), _validador, Dados.Agora);
        var r = v.AdicionarFonte(Dados.Fonte(), _validador, Dados.Agora);
        Assert.Contains(r.Erros, e => e.Codigo == "fonte.codigo_duplicado");
    }

    [Fact]
    public void Recusa_fonte_oficial_fora_de_dominio_oficial()
    {
        var r = Dados.Versao().AdicionarFonte(Dados.Fonte(link: "https://www.ioerj.com.br/"), _validador, Dados.Agora);
        Assert.Contains(r.Erros, e => e.Codigo == "fonte.dominio_nao_oficial");
    }

    [Fact]
    public void Aceita_dominio_nao_oficial_como_agregador()
    {
        var r = Dados.Versao().AdicionarFonte(Dados.Fonte(link: "https://queridodiario.ok.org.br/", natureza: NaturezaFonte.AgregadorApoio), _validador, Dados.Agora);
        Assert.True(r.Sucesso);
    }

    [Fact]
    public void Excecao_de_dominio_exige_aprovador()
    {
        var v = Dados.Versao();
        Assert.False(v.AdicionarFonte(Dados.Fonte(codigo: "EST-RJ-DOE", link: "https://www.ioerj.com.br/", excecao: null), _validador, Dados.Agora).Sucesso);
        Assert.True(v.AdicionarFonte(Dados.Fonte(codigo: "EST-RJ-DOE", link: "https://www.ioerj.com.br/", excecao: "coordenador"), _validador, Dados.Agora).Sucesso);
    }

    [Fact]
    public void Fonte_estadual_exige_ente()
    {
        var r = Dados.Versao().AdicionarFonte(Dados.Fonte(esfera: EsferaFonte.Estadual), _validador, Dados.Agora);
        Assert.False(r.Sucesso);
        Assert.True(Dados.Versao().AdicionarFonte(Dados.Fonte(esfera: EsferaFonte.Estadual, ente: "SP"), _validador, Dados.Agora).Sucesso);
    }

    [Fact]
    public void Orgao_emissor_obrigatorio()
    {
        var r = Dados.Versao().AdicionarFonte(Dados.Fonte(orgao: " "), _validador, Dados.Agora);
        Assert.Contains(r.Erros, e => e.Codigo == "fonte.orgao_obrigatorio");
    }

    [Fact]
    public void Versao_exige_aprovador() => Assert.False(VersaoCatalogo.Criar("v", "doc", "", Dados.Agora).Sucesso);
}

public class EnteMonitoradoTests
{
    [Fact]
    public void Nao_ativa_sem_fontes()
    {
        var e = EnteMonitorado.Criar(TipoEnte.Municipio, Ente.Parse("SP-Campinas").Valor, Dados.Agora).Valor;
        Assert.False(e.Ativar(0, Dados.Agora).Sucesso);
        Assert.False(e.Ativo);
        Assert.True(e.Ativar(2, Dados.Agora).Sucesso);
        Assert.True(e.Ativo);
    }

    [Fact]
    public void Municipio_exige_nome() =>
        Assert.False(EnteMonitorado.Criar(TipoEnte.Municipio, Ente.Parse("SP").Valor, Dados.Agora).Sucesso);
}

public class AgendaColetaTests
{
    private readonly AgendaColeta _agenda = new();
    private static readonly DateOnly Quinta = new(2026, 10, 29);
    private static readonly DateOnly Sabado = new(2026, 10, 31);

    [Fact]
    public void Diaria_dias_uteis_nao_roda_no_fim_de_semana() =>
        Assert.False(_agenda.EstaDevida(Periodicidade.DiariaDiasUteis, null, Sabado));

    [Fact]
    public void Sem_historico_esta_devida() => Assert.True(_agenda.EstaDevida(Periodicidade.Semanal, null, Quinta));

    [Theory]
    [InlineData(Periodicidade.Diaria, 1, true)]
    [InlineData(Periodicidade.Diaria, 0, false)]
    [InlineData(Periodicidade.Semanal, 6, false)]
    [InlineData(Periodicidade.Semanal, 7, true)]
    [InlineData(Periodicidade.Quinzenal, 15, true)]
    [InlineData(Periodicidade.Trimestral, 89, false)]
    public void Respeita_periodicidade(Periodicidade p, int diasDesde, bool devida) =>
        Assert.Equal(devida, _agenda.EstaDevida(p, Quinta.AddDays(-diasDesde), Quinta));
}
