using Agente.Domain.Operacao;
using Agente.Domain.Tests.Catalogo;

namespace Agente.Domain.Tests.Operacao;

public class OperacaoTests
{
    [Fact]
    public void Consumo_desconhecido_fica_nulo_e_nao_zero()
    {
        var r = RegistroConsumo.Registrar(Guid.NewGuid(), "c", "openai", "m", FinalidadeLlm.Conversa, 1, null, null, Dados.Agora, TimeSpan.FromSeconds(1), "falha", null, Dados.Agora);
        Assert.False(r.UsoConhecido);
        Assert.Null(r.TokensEntrada);
        Assert.Null(r.CustoEstimado);
    }

    [Fact]
    public void Consumo_com_tabela_estima_custo()
    {
        var tabela = TabelaPreco.Criar("m", 2m, 8m, "USD", Dados.Agora, "https://openai.com/api/pricing", Dados.Agora).Valor;
        var r = RegistroConsumo.Registrar(Guid.NewGuid(), "c", "openai", "m", FinalidadeLlm.Classificacao, 1, 1_000_000, 500_000, Dados.Agora, TimeSpan.Zero, "sucesso", tabela, Dados.Agora);
        Assert.True(r.UsoConhecido);
        Assert.Equal(6m, r.CustoEstimado);
        Assert.Equal("USD", r.Moeda);
    }

    [Fact]
    public void Periodo_invalido()
    {
        Assert.False(PeriodoConsulta.Criar(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)).Sucesso);
        var p = PeriodoConsulta.UltimosDias(new DateOnly(2026, 10, 29), 7);
        Assert.Equal(new DateOnly(2026, 10, 23), p.Inicio);
    }

    [Fact]
    public void Consulta_registra_etapas()
    {
        var c = Consulta.Iniciar("c", "u", null, "NF-e", PeriodoConsulta.UltimosDias(new DateOnly(2026, 10, 29), 7), [], [], [], "1.0", Dados.Agora);
        var e = c.IniciarEtapa(EtapaFluxo.Coleta, Dados.Agora);
        e.Finalizar(StatusEtapa.Sucesso, null, Dados.Agora);
        c.Finalizar(StatusProcessamento.Concluido, false, ["x"], Dados.Agora);
        Assert.Single(c.Etapas);
        Assert.Equal(["x"], c.Limitacoes);
    }
}
