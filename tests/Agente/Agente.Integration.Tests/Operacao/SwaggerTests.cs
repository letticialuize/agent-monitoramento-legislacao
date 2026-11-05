using System.Net;
using Agente.Integration.Tests.Infra;

namespace Agente.Integration.Tests.Operacao;

/// <summary>A API está sem login (ADR 0007): o Swagger não pode aparecer fora de Development.</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class SwaggerTests(ContainersFixture containers) : IDisposable
{
    private readonly AgenteApp _app = new(containers);

    public void Dispose() => _app.Dispose();

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Fora_de_development_nao_expoe_swagger(string rota)
    {
        using var cliente = _app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync(rota)).StatusCode);
    }
}
