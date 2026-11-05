using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Ferramentas;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Domain.Catalogo;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agente.Application.Tests.Monitoramento;

public class ConsultarNovidadesTests
{
    [Fact]
    public async Task Sem_novidades_informa_ausencia_sem_afirmar_conformidade()
    {
        var (caso, _, _, consultas) = Montagem.Consulta();
        var r = await caso.ExecutarAsync(new PedidoConsulta("NF-e"), Montagem.Consultor, "c1", null, default);

        var env = r.Valor;
        Assert.Equal(ConsultarNovidades.MensagemSemNovidades, env.Resultado!.MensagemSemNovidades);
        Assert.Contains(env.Achados, a => a.Categoria == CategoriaAchado.AusenciaEvidencia);
        Assert.Equal(StatusProcessamento.Concluido, env.Status);
        Assert.Equal(["FED-SENADO"], env.Resultado.FontesConsultadas);
        Assert.Single(consultas.Itens);
        Assert.Contains(consultas.Itens[0].Etapas, e => e.Etapa == EtapaFluxo.Llm && e.Status == StatusEtapa.NaoExecutada);
    }

    [Fact]
    public async Task Sem_perfil_informado_consulta_o_consultor_tributario()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        await caso.ExecutarAsync(new PedidoConsulta("CBS"), Montagem.Consultor, "c", null, default);
        Assert.Equal([Perfil.ConsultorTributario], leitura.UltimoFiltro!.Perfis);

        await caso.ExecutarAsync(new PedidoConsulta("CBS", Perfis: []), Montagem.Consultor, "c", null, default);
        Assert.Equal([Perfil.ConsultorTributario], leitura.UltimoFiltro!.Perfis);
    }

    [Fact]
    public async Task Perfil_desenvolvedor_informado_filtra_so_desenvolvedor()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        await caso.ExecutarAsync(new PedidoConsulta("CBS", Perfis: [Perfil.Desenvolvedor]), Montagem.Consultor, "c", null, default);
        Assert.Equal([Perfil.Desenvolvedor], leitura.UltimoFiltro!.Perfis);
    }

    [Fact]
    public async Task Fonte_com_falha_gera_falha_tecnica_e_limitacao()
    {
        var (caso, leitura, fontes, _) = Montagem.Consulta();
        leitura.Itens.Add(Montagem.Novidade());
        fontes.Situacao = new SituacaoFontes(["FED-SENADO", "FED-DOU"], [new FonteComFalha("FED-DOU", "503")], []);

        var env = (await caso.ExecutarAsync(new PedidoConsulta("CBS"), Montagem.Consultor, "c", null, default)).Valor;
        Assert.Equal(StatusProcessamento.ConcluidoComLimitacoes, env.Status);
        Assert.Contains(env.Achados, a => a.Categoria == CategoriaAchado.FalhaTecnica && a.Referencia == "FED-DOU");
        Assert.Equal(["FED-DOU"], env.Resultado!.FontesComFalha);
    }

    [Fact]
    public async Task Pendente_e_indicio_da_ia_marcam_revisao_humana()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        leitura.Itens.Add(Montagem.Novidade(SituacaoNovidade.PendenteConfirmacao));
        leitura.Itens.Add(Montagem.Novidade(origemPerfil: Origem.IndicioIa));

        var env = (await caso.ExecutarAsync(new PedidoConsulta("CBS"), Montagem.Consultor, "c", null, default)).Valor;
        Assert.True(env.RequerRevisaoHumana);
        Assert.Contains(env.Achados, a => a.Categoria == CategoriaAchado.IndicioIa);
        Assert.Contains(env.Achados, a => a.Categoria == CategoriaAchado.AusenciaEvidencia);
    }

    [Fact]
    public async Task Corte_por_limite_informa_itens_omitidos()
    {
        var (caso, leitura, _, _) = Montagem.Consulta(limite: 2);
        for (var i = 0; i < 5; i++)
        {
            leitura.Itens.Add(Montagem.Novidade());
        }

        var env = (await caso.ExecutarAsync(new PedidoConsulta("imposto"), Montagem.Consultor, "c", null, default)).Valor;
        Assert.Equal(3, env.Resultado!.ItensOmitidos);
        Assert.Equal(StatusProcessamento.ConcluidoComLimitacoes, env.Status);
    }

    [Fact]
    public async Task Assunto_expande_sinonimos()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        await caso.ExecutarAsync(new PedidoConsulta("CBS"), Montagem.Consultor, "c", null, default);
        Assert.Contains("Contribuição sobre Bens e Serviços", leitura.UltimoFiltro!.Termos);
    }

    [Theory]
    [InlineData("a", null)]
    [InlineData("NF-e", "Cliente X")]
    public async Task Entrada_invalida_ou_cliente_e_recusada(string assunto, string? cliente)
    {
        var (caso, _, _, consultas) = Montagem.Consulta();
        var r = await caso.ExecutarAsync(new PedidoConsulta(assunto, ClienteNome: cliente), Montagem.Consultor, "c", null, default);
        Assert.False(r.Sucesso);
        Assert.Empty(consultas.Itens);
    }
}

public class FerramentasNovidadesTests
{
    private static (FerramentasNovidades Ferramentas, LeituraFake Leitura, Consulta Pai) Montar()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        var relogio = new RelogioFixo();
        var f = new FerramentasNovidades(caso, leitura, new RegistradorEtapas(relogio), Montagem.Opcoes(new OpcoesFerramentas()), NullLogger<FerramentasNovidades>.Instance);
        var pai = Consulta.Iniciar("c", "u", "conv", "x", PeriodoConsulta.UltimosDias(relogio.HojeLocal, 7), [], [], [], "1.0", relogio.AgoraUtc);
        return (f, leitura, pai);
    }

    [Fact]
    public async Task Usuario_sem_papel_e_recusado_antes_de_executar()
    {
        var (f, leitura, pai) = Montar();
        var coletor = new ColetorFerramentas();
        var buscar = f.Criar(new ContextoUsuario("u", [Papeis.Revisor]), "c", "conv", pai, coletor)
            .Single(d => d.Nome == FerramentasNovidades.BuscarNovidades);

        var saida = await buscar.Executar(new Dictionary<string, string?> { ["assunto"] = "CBS" }, default);
        Assert.Contains("nao_autorizado", saida, StringComparison.Ordinal);
        Assert.Null(leitura.UltimoFiltro);
        Assert.Contains(pai.Etapas, e => e.Etapa == EtapaFluxo.Ferramentas && e.Status == StatusEtapa.Falha);
    }

    [Theory]
    [InlineData("esferas", "galactica")]
    [InlineData("dataInicio", "29/10/2026")]
    public async Task Argumentos_invalidos_nao_executam(string campo, string valor)
    {
        var (f, leitura, pai) = Montar();
        var buscar = f.Criar(Montagem.Consultor, "c", "conv", pai, new ColetorFerramentas()).Single(d => d.Nome == FerramentasNovidades.BuscarNovidades);
        var saida = await buscar.Executar(new Dictionary<string, string?> { ["assunto"] = "CBS", [campo] = valor }, default);
        Assert.Contains("argumentos_invalidos", saida, StringComparison.Ordinal);
        Assert.Null(leitura.UltimoFiltro);
    }

    [Fact]
    public async Task Novidade_descartada_nao_e_retornada()
    {
        var (f, leitura, pai) = Montar();
        var descartada = Montagem.Novidade(SituacaoNovidade.Descartada);
        leitura.Itens.Add(descartada);
        var obter = f.Criar(Montagem.Consultor, "c", "conv", pai, new ColetorFerramentas()).Single(d => d.Nome == FerramentasNovidades.ObterNovidade);
        var saida = await obter.Executar(new Dictionary<string, string?> { ["novidadeId"] = descartada.Id.ToString() }, default);
        Assert.Contains("nao_encontrada", saida, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Busca_registra_novidades_no_coletor()
    {
        var (f, leitura, pai) = Montar();
        leitura.Itens.Add(Montagem.Novidade());
        var coletor = new ColetorFerramentas();
        var buscar = f.Criar(Montagem.Consultor, "c", "conv", pai, coletor).Single(d => d.Nome == FerramentasNovidades.BuscarNovidades);
        await buscar.Executar(new Dictionary<string, string?> { ["assunto"] = "CBS", ["perfis"] = "consultor_tributario" }, default);
        Assert.Single(coletor.Novidades);
        Assert.Contains("https://www.in.gov.br/ato-1", coletor.UrlsRetornadas);
    }
}

public class ValidacaoCitacoesTests
{
    [Fact]
    public async Task Link_ou_id_inventado_viola()
    {
        var (caso, leitura, _, _) = Montagem.Consulta();
        var n = Montagem.Novidade();
        leitura.Itens.Add(n);
        var coletor = new ColetorFerramentas();
        var relogio = new RelogioFixo();
        var f = new FerramentasNovidades(caso, leitura, new RegistradorEtapas(relogio), Montagem.Opcoes(new OpcoesFerramentas()), NullLogger<FerramentasNovidades>.Instance);
        var pai = Consulta.Iniciar("c", "u", "conv", "x", PeriodoConsulta.UltimosDias(relogio.HojeLocal, 7), [], [], [], "1.0", relogio.AgoraUtc);
        await f.Criar(Montagem.Consultor, "c", "conv", pai, coletor).Single(d => d.Nome == FerramentasNovidades.BuscarNovidades)
            .Executar(new Dictionary<string, string?> { ["assunto"] = "CBS" }, default);

        Assert.Empty(ResponderMensagemConversa.ValidarCitacoes($"Veja https://www.in.gov.br/ato-1. (id {n.Id})", coletor));
        Assert.NotEmpty(ResponderMensagemConversa.ValidarCitacoes("Veja https://www.in.gov.br/ato-inventado", coletor));
        Assert.NotEmpty(ResponderMensagemConversa.ValidarCitacoes($"id {Guid.NewGuid()}", coletor));
    }
}

public class NaturezaOcorrenciaTests
{
    private static Fonte Fonte(NaturezaFonte natureza)
    {
        var v = VersaoCatalogo.Criar("v", "d", "a", DateTimeOffset.UtcNow).Valor;
        v.AdicionarFonte(new DadosFonte("FED-SENADO", "S", natureza == NaturezaFonte.Oficial ? "https://legis.senado.leg.br/" : "https://queridodiario.ok.org.br/", "x", "Senado", EsferaFonte.Federal, null,
            Grupo.LegislacaoFederal, natureza, [PapelResponsavel.AJ], [Formato.Json], TipoConector.SenadoDadosAbertos, null, Periodicidade.Diaria), new ValidadorConfiabilidadeFonte(), DateTimeOffset.UtcNow);
        return v.Fontes[0];
    }

    private static readonly HashSet<string> Hosts = ["legis.senado.leg.br"];

    [Theory]
    [InlineData("https://legis.senado.leg.br/norma/1", true)]
    [InlineData("https://normas.leg.br/?urn=x", true)]
    [InlineData("https://blog-qualquer.com/post", false)]
    public void Fonte_oficial_classifica_link_do_item(string url, bool oficial)
    {
        var (fonteId, natureza) = ColetarFonte.NaturezaDaOcorrencia(Fonte(NaturezaFonte.Oficial), url, Hosts);
        Assert.Equal(oficial, natureza == NaturezaFonte.Oficial);
        Assert.Equal(oficial, fonteId is not null);
    }

    [Fact]
    public void Agregador_e_sempre_apoio() =>
        Assert.Equal(NaturezaFonte.AgregadorApoio, ColetarFonte.NaturezaDaOcorrencia(Fonte(NaturezaFonte.AgregadorApoio), "https://www.in.gov.br/x", Hosts).Natureza);
}
