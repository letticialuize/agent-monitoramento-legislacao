using Agente.Domain.Catalogo;
using Agente.Integration.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace Agente.Integration.Tests.Conectores;

/// <summary>O catálogo versionado em docs/fontes passa pelos critérios de confiabilidade do domínio.</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class CatalogoRealTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public Task InitializeAsync()
    {
        var real = Path.Combine(AppContext.BaseDirectory, "Catalogo", "catalogo-2026-10-29.json");
        Assert.True(File.Exists(real), real);
        _app = new AgenteApp(containers, extras: new() { ["Coleta:ArquivoCatalogoInicial"] = real });
        _app.CreateClient().Dispose();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Catalogo_real_e_importado_e_ativado()
    {
        var fontes = await _app.NoBanco(db => db.Fontes.ToListAsync());
        Assert.Equal(26, fontes.Count);
        Assert.True(await _app.NoBanco(db => db.VersoesCatalogo.AnyAsync(v => v.Versao == "2026-10-29" && v.Vigente)));
        Assert.Equal("AGR-QUERIDO-DIARIO", Assert.Single(fontes, f => f.Natureza == Domain.Comum.NaturezaFonte.AgregadorApoio).Codigo.Valor);
    }
}
