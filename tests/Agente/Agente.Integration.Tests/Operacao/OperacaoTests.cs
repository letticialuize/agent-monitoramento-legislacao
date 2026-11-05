using Agente.Application.Operacao;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Agente.Integration.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Operacao;

/// <summary>Fechamento da Entrega 1: retenção (cenário 14), consumo desconhecido (cenário 13) e logs.</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class OperacaoTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public async Task InitializeAsync()
    {
        _app = new AgenteApp(containers, llmConfigurado: true);
        Simuladores.Fontes(_app.Fontes);
        Simuladores.ClassificacaoSemUso(_app.OpenAi);
        _app.CreateClient().Dispose();
        await _app.ColetarAsync();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Cenario13_uso_nao_informado_fica_nulo_e_nao_zero()
    {
        var registros = await _app.NoBanco(db => db.RegistrosConsumo.ToListAsync());
        Assert.NotEmpty(registros);
        Assert.All(registros, r =>
        {
            Assert.False(r.UsoConhecido);
            Assert.Null(r.TokensEntrada);
            Assert.Null(r.TokensSaida);
            Assert.Null(r.CustoEstimado);
        });

        // Saída inválida (ids ausentes) → 1 tentativa + 2 retentativas registradas.
        Assert.Equal(3, registros.Count(r => r.Finalidade == FinalidadeLlm.Classificacao));
        var novidades = await _app.NoBanco(db => db.Novidades.ToListAsync());
        Assert.All(novidades, n => Assert.False(n.AguardandoClassificacaoIa));
    }

    [Fact]
    public async Task Cenario14_retencao_remove_dados_operacionais_e_preserva_revisao_aberta_e_catalogo()
    {
        using (var escopo = _app.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<Agente.Infrastructure.Persistencia.AgenteDbContext>();
            var pendente = await db.Novidades.FirstAsync(n => n.Situacao == SituacaoNovidade.PendenteConfirmacao);
            Assert.True(pendente.ColocarEmAnalise("revisor-1", "conferindo no DOM", _app.Tempo.GetUtcNow()).Sucesso);
            await db.SaveChangesAsync();
        }

        _app.Tempo.Advance(TimeSpan.FromDays(31));
        using (var escopo = _app.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<AplicarRetencao>().ExecutarAsync(CancellationToken.None);
        }

        var restantes = await _app.NoBanco(db => db.Novidades.ToListAsync());
        var mantida = Assert.Single(restantes);
        Assert.Equal(SituacaoNovidade.EmAnalise, mantida.Situacao);
        Assert.Single(mantida.Revisoes);
        Assert.Equal(0, await _app.NoBanco(db => db.ExecucoesColeta.CountAsync()));
        Assert.Equal(0, await _app.NoBanco(db => db.RegistrosConsumo.CountAsync()));
        Assert.True(await _app.NoBanco(db => db.Fontes.AnyAsync()));
        Assert.True(await _app.NoBanco(db => db.VersoesCatalogo.AnyAsync(v => v.Vigente)));
    }

    [Fact]
    public async Task Logs_tem_correlacao_e_nao_contem_segredos()
    {
        using var cliente = _app.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-teste-123");
        var resposta = await cliente.PostAsync("/api/v1/consultas", new StringContent("""{"assunto":"CBS"}""", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal("corr-teste-123", resposta.Headers.GetValues("X-Correlation-Id").Single());

        var linhas = _app.Logs.Linhas.ToList();
        Assert.NotEmpty(linhas);
        Assert.DoesNotContain(linhas, l => l.Contains(AgenteApp.ChaveLlm, StringComparison.Ordinal));
        Assert.DoesNotContain(linhas, l => l.Contains("Password=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(linhas, l => l.Contains("execução", StringComparison.Ordinal) || l.Contains("ExecucaoId", StringComparison.Ordinal) || l.Contains("Ciclo", StringComparison.Ordinal) || l.Contains("Saída do LLM inválida", StringComparison.Ordinal));
    }
}
