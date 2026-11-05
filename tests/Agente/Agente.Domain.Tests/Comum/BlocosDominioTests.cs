using Agente.Domain.Comum;

namespace Agente.Domain.Tests.Comum;

public class BlocosDominioTests
{
    private sealed record Valor(int X) : ObjetoDeValor;

    [Fact]
    public void Objeto_de_valor_compara_por_valor() => Assert.Equal(new Valor(1), new Valor(1));

    [Fact]
    public void Resultado_com_falha_nao_tem_valor()
    {
        var r = Resultado<int>.Falha("x", "erro");
        Assert.False(r.Sucesso);
        Assert.Throws<InvalidOperationException>(() => r.Valor);
    }

    [Fact]
    public void Resultado_ok_expoe_valor() => Assert.Equal(5, Resultado<int>.Ok(5).Valor);

    [Theory]
    [InlineData("  Nota   Técnica  ", "nota tecnica")]
    [InlineData("ICMS-ST", "icms-st")]
    [InlineData(null, "")]
    public void Normaliza_texto(string? entrada, string esperado) => Assert.Equal(esperado, Texto.Normalizar(entrada));

    [Fact]
    public void Contem_termo_respeita_limite_de_palavra()
    {
        Assert.True(Texto.ContemTermo("altera o leiaute da nf-e", "Leiaute"));
        Assert.False(Texto.ContemTermo("rapido", "api"));
        Assert.True(Texto.ContemTermo("nova api de consulta", "API"));
    }
}
