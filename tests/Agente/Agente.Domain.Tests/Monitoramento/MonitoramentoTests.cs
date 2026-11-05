using Agente.Domain.Catalogo;
using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Tests.Catalogo;

namespace Agente.Domain.Tests.Monitoramento;

internal static class Novidades
{
    public static DadosNovidade Dados(string? numero = "2.250", string ementa = "Altera a IN sobre NF-e") =>
        new(new DateOnly(2026, 10, 28), "Receita Federal", "Instrução Normativa", numero, ementa, null);

    public static Novidade Nova(NaturezaFonte? natureza = NaturezaFonte.Oficial, string url = "https://normas.receita.fazenda.gov.br/a")
    {
        var d = Dados();
        var n = Novidade.Criar(d, new DeduplicadorNovidades().GerarChave(d), "hash", Catalogo.Dados.Agora).Valor;
        n.RegistrarOcorrencia(natureza is null ? null : Guid.NewGuid(), natureza, url, Guid.NewGuid(), Catalogo.Dados.Agora);
        n.DefinirSituacaoInicial(new ClassificadorSituacao().Classificar(n.Ocorrencias.Select(o => o.Natureza)), Catalogo.Dados.Agora);
        n.AplicarClassificacao(new ClassificacaoDeterministica(Esfera.Federal, null, Grupo.Tributos, [Perfil.ConsultorTributario], true, [PapelResponsavel.AF]), Catalogo.Dados.Agora);
        return n;
    }

    public static LinkOficial Link(string url = "https://www.in.gov.br/ato") => LinkOficial.Criar(url).Valor;
}

public class ChaveDeduplicacaoTests
{
    [Fact]
    public void Mesmo_ato_com_numero_formatado_diferente_gera_mesma_chave()
    {
        var a = ChaveDeduplicacao.Criar("Receita Federal", "Instrução Normativa", "Nº 2.250", new DateOnly(2026, 1, 2), "x");
        var b = ChaveDeduplicacao.Criar("RECEITA FEDERAL", "instrucao normativa", "2250", new DateOnly(2026, 5, 9), "y");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Sem_numero_usa_hash_da_ementa_orgao_e_data()
    {
        var a = ChaveDeduplicacao.Criar("TJSP", "Ato", null, new DateOnly(2026, 1, 2), "Texto  igual");
        var b = ChaveDeduplicacao.Criar("TJSP", "Ato", "", new DateOnly(2026, 1, 2), "texto igual");
        var c = ChaveDeduplicacao.Criar("TJSP", "Ato", null, new DateOnly(2026, 1, 3), "texto igual");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("h|", a.Valor, StringComparison.Ordinal);
    }
}

public class ClassificadorSituacaoTests
{
    private readonly ClassificadorSituacao _c = new();

    [Fact]
    public void Oficial_confirma() => Assert.Equal(SituacaoNovidade.Confirmada, _c.Classificar([NaturezaFonte.AgregadorApoio, NaturezaFonte.Oficial]));

    [Fact]
    public void So_agregador_fica_pendente() => Assert.Equal(SituacaoNovidade.PendenteConfirmacao, _c.Classificar([NaturezaFonte.AgregadorApoio]));

    [Fact]
    public void Fora_do_catalogo() => Assert.Equal(SituacaoNovidade.FonteNaoCatalogada, _c.Classificar([null]));
}

public class NovidadeTests
{
    [Fact]
    public void Confirmada_por_fonte_oficial_recebe_link()
    {
        var n = Novidades.Nova();
        Assert.Equal(SituacaoNovidade.Confirmada, n.Situacao);
        Assert.Equal("https://normas.receita.fazenda.gov.br/a", n.LinkOficial);
        Assert.False(n.RequerRevisaoHumana);
    }

    [Fact]
    public void Ocorrencia_oficial_passa_a_ser_principal()
    {
        var d = Novidades.Dados();
        var n = Novidade.Criar(d, new DeduplicadorNovidades().GerarChave(d), "h", Dados.Agora).Valor;
        n.RegistrarOcorrencia(Guid.NewGuid(), NaturezaFonte.AgregadorApoio, "https://agregador/x", Guid.NewGuid(), Dados.Agora);
        n.RegistrarOcorrencia(Guid.NewGuid(), NaturezaFonte.Oficial, "https://www.in.gov.br/x", Guid.NewGuid(), Dados.Agora);
        Assert.Single(n.Ocorrencias, o => o.Principal);
        Assert.Equal("https://www.in.gov.br/x", n.Ocorrencias.Single(o => o.Principal).Url);
    }

    [Fact]
    public void Ocorrencia_repetida_nao_duplica()
    {
        var n = Novidades.Nova();
        var o = n.Ocorrencias[0];
        n.RegistrarOcorrencia(o.FonteId, o.Natureza, o.Url, Guid.NewGuid(), Dados.Agora);
        Assert.Single(n.Ocorrencias);
    }

    [Fact]
    public void Nao_confirma_sem_ocorrencia_oficial()
    {
        var d = Novidades.Dados();
        var n = Novidade.Criar(d, new DeduplicadorNovidades().GerarChave(d), "h", Dados.Agora).Valor;
        n.RegistrarOcorrencia(null, null, "https://x.com/a", Guid.NewGuid(), Dados.Agora);
        Assert.False(n.DefinirSituacaoInicial(SituacaoNovidade.Confirmada, Dados.Agora).Sucesso);
    }

    [Fact]
    public void Pendente_requer_revisao_e_pode_ser_confirmada_com_link_oficial()
    {
        var n = Novidades.Nova(NaturezaFonte.AgregadorApoio, "https://data.queridodiario.ok.org.br/x.pdf");
        Assert.Equal(SituacaoNovidade.PendenteConfirmacao, n.Situacao);
        Assert.True(n.RequerRevisaoHumana);

        Assert.False(n.Confirmar(LinkOficial.Criar("https://x.com/a").Valor, "rev", "conferido", Dados.Agora).Sucesso);
        Assert.True(n.Confirmar(Novidades.Link(), "rev", "conferido no DOU", Dados.Agora).Sucesso);
        Assert.Equal(SituacaoNovidade.Confirmada, n.Situacao);
        Assert.Single(n.Revisoes);
    }

    [Fact]
    public void Revisao_exige_motivo_e_estado_revisavel()
    {
        var pendente = Novidades.Nova(null, "https://x.com/a");
        Assert.Equal(SituacaoNovidade.FonteNaoCatalogada, pendente.Situacao);
        Assert.False(pendente.Descartar("rev", "", Dados.Agora).Sucesso);
        Assert.True(pendente.ColocarEmAnalise("rev", "verificando", Dados.Agora).Sucesso);
        Assert.False(pendente.ColocarEmAnalise("rev", "de novo", Dados.Agora).Sucesso);
        Assert.True(pendente.Descartar("rev", "não é ato oficial", Dados.Agora).Sucesso);
        Assert.Equal(SituacaoNovidade.Descartada, pendente.Situacao);

        var confirmada = Novidades.Nova();
        Assert.False(confirmada.Descartar("rev", "motivo", Dados.Agora).Sucesso);
    }

    [Fact]
    public void Perfil_obrigatorio()
    {
        var d = Novidades.Dados();
        var n = Novidade.Criar(d, new DeduplicadorNovidades().GerarChave(d), "h", Dados.Agora).Valor;
        Assert.False(n.AplicarClassificacao(new ClassificacaoDeterministica(Esfera.Federal, null, Grupo.Tributos, [], true, []), Dados.Agora).Sucesso);
    }

    [Fact]
    public void Sugestao_da_ia_so_substitui_perfil_nao_decidido()
    {
        var decidida = Novidades.Nova();
        decidida.AplicarSugestaoIa([Perfil.Desenvolvedor], "NF-e", Impacto.Alto, Dados.Agora);
        Assert.Equal([Perfil.ConsultorTributario], decidida.Perfis);
        Assert.Equal(Origem.RegraDeterministica, decidida.OrigemPerfil);
        Assert.Equal(Impacto.Alto, decidida.Impacto);
        Assert.Equal(Origem.IndicioIa, decidida.OrigemImpacto);
        Assert.False(decidida.AguardandoClassificacaoIa);

        var d = Novidades.Dados();
        var indefinida = Novidade.Criar(d, new DeduplicadorNovidades().GerarChave(d), "h", Dados.Agora).Valor;
        indefinida.RegistrarOcorrencia(Guid.NewGuid(), NaturezaFonte.Oficial, "https://www.in.gov.br/a", Guid.NewGuid(), Dados.Agora);
        indefinida.AplicarClassificacao(new ClassificacaoDeterministica(Esfera.Federal, null, Grupo.Tributos, [Perfil.ConsultorTributario, Perfil.Desenvolvedor], false, []), Dados.Agora);
        indefinida.AplicarSugestaoIa([Perfil.Desenvolvedor], null, null, Dados.Agora);
        Assert.Equal([Perfil.Desenvolvedor], indefinida.Perfis);
        Assert.Equal(Origem.IndicioIa, indefinida.OrigemPerfil);
        Assert.True(indefinida.RequerRevisaoHumana);
    }
}

public class ResolvedorEsferaTests
{
    private static readonly RegrasEsferaPorOrgao Regras = new("t", Esfera.Federal,
    [
        new("Município", Esfera.Municipal, null),
        new("TJ*", Esfera.Estadual, null),
        new("TRE", Esfera.Federal, null),
        new("CONFAZ", Esfera.Estadual, null),
    ]);

    private static Fonte Fonte(EsferaFonte esfera, string? ente = null)
    {
        var v = Dados.Versao();
        v.AdicionarFonte(Dados.Fonte(esfera: esfera, ente: ente), new ValidadorConfiabilidadeFonte(), Dados.Agora);
        return v.Fontes[0];
    }

    [Fact]
    public void Herda_esfera_da_fonte()
    {
        var r = new ResolvedorEsfera().Resolver(Fonte(EsferaFonte.Estadual, "SP"), "qualquer", Regras);
        Assert.Equal(Esfera.Estadual, r.Esfera);
        Assert.Equal("SP", r.Ente!.Uf);
    }

    [Theory]
    [InlineData("TJSP - 1ª Vara", Esfera.Estadual)]
    [InlineData("Município de Três Arroios/RS", Esfera.Municipal)]
    [InlineData("CONFAZ", Esfera.Estadual)]
    [InlineData("TRE-SP", Esfera.Federal)]
    public void Fonte_nacional_usa_regra_por_orgao(string orgao, Esfera esperada)
    {
        var r = new ResolvedorEsfera().Resolver(Fonte(EsferaFonte.Nacional), orgao, Regras);
        Assert.Equal(esperada, r.Esfera);
        Assert.Null(r.Limitacao);
    }

    [Fact]
    public void Orgao_sem_regra_usa_padrao_com_limitacao()
    {
        var r = new ResolvedorEsfera().Resolver(Fonte(EsferaFonte.Nacional), "CGIBS", Regras);
        Assert.Equal(Esfera.Federal, r.Esfera);
        Assert.NotNull(r.Limitacao);
    }
}

public class ClassificadorPerfilTests
{
    private static readonly RegrasPerfil Regras = new("t", ["TRIB-NFE"], ["leiaute", "nota técnica", "API"], [Grupo.Tributos, Grupo.LegislacaoFederal], []);
    private readonly ClassificadorPerfilDeterministico _c = new();

    [Fact]
    public void Fonte_de_obrigacoes_acessorias_e_desenvolvedor()
    {
        var r = _c.Classificar("TRIB-NFE", Grupo.Tributos, "Nota Técnica", "Publicada NT 2026.001", Regras);
        Assert.Equal([Perfil.Desenvolvedor], r.Perfis);
        Assert.True(r.Decidido);
    }

    [Fact]
    public void Legislacao_com_termo_de_sistema_atende_os_dois_perfis()
    {
        var r = _c.Classificar("TRIB-RFB", Grupo.Tributos, "Instrução Normativa", "Altera o leiaute da EFD", Regras);
        Assert.Equal([Perfil.ConsultorTributario, Perfil.Desenvolvedor], r.Perfis);
    }

    [Fact]
    public void Legislacao_sem_termo_tecnico_e_consultor() =>
        Assert.Equal([Perfil.ConsultorTributario], _c.Classificar("FED-SENADO", Grupo.LegislacaoFederal, "Lei", "Altera alíquota", Regras).Perfis);

    [Fact]
    public void Sem_regra_aplicavel_devolve_ambos_nao_decidido()
    {
        var r = _c.Classificar("X", Grupo.TribunaisJudiciais, "Acórdão", "Decisão", Regras);
        Assert.False(r.Decidido);
        Assert.Equal(2, r.Perfis.Count);
    }
}
