using System.Net;
using Agente.Integration.Tests.Infra;

namespace Agente.Integration.Tests.Operacao;

/// <summary>
/// A API está sem login (ADR 0007): fora de Development, o Swagger só aparece com Swagger:Habilitado=true.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class SwaggerTests(ContainersFixture containers)
{
    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Fora_de_development_sem_configuracao_nao_expoe_swagger(string rota)
    {
        using var app = new AgenteApp(containers);
        using var cliente = app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync(rota)).StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Fora_de_development_com_swagger_habilitado_expoe_swagger(string rota)
    {
        using var app = new AgenteApp(containers, extras: new() { ["Swagger:Habilitado"] = "true" });
        using var cliente = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(rota)).StatusCode);
    }
}
