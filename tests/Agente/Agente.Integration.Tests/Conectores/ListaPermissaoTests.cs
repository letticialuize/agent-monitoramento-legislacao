using Agente.Infrastructure.Conectores;
using Agente.Integration.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Conectores;

/// <summary>T043 — o cliente dos conectores só acessa hosts derivados do catálogo vigente (Seção 5.1).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ListaPermissaoTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public Task InitializeAsync()
    {
        _app = new AgenteApp(containers);
        Simuladores.Fontes(_app.Fontes);
        _app.CreateClient().Dispose();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Cliente() => _app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("conectores");

    [Fact]
    public async Task Host_do_catalogo_e_permitido()
    {
        using var resposta = await Cliente().GetAsync(new Uri($"{_app.Fontes.Url}/camara/api/v2/proposicoes"));
        Assert.True(resposta.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Host_fora_do_catalogo_e_bloqueado_antes_da_requisicao()
    {
        await Assert.ThrowsAsync<DestinoNaoPermitidoException>(() => Cliente().GetAsync(new Uri("https://exemplo-nao-catalogado.com/")));
    }
}
