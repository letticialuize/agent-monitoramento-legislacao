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

    [Fact]
    public async Task Com_swagger_habilitado_a_raiz_redireciona_para_o_swagger()
    {
        using var app = new AgenteApp(containers, extras: new() { ["Swagger:Habilitado"] = "true" });
        using var cliente = app.CreateClient(new() { AllowAutoRedirect = false });
        var resposta = await cliente.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/swagger", resposta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task OpenAPI_declara_o_Bearer_da_SOFIA_so_nas_rotas_A2A_e_traz_exemplo_de_message_send()
    {
        using var app = new AgenteApp(containers, extras: new() { ["Swagger:Habilitado"] = "true" });
        using var cliente = app.CreateClient();

        var texto = await cliente.GetStringAsync("/openapi/v1.json");
        var doc = System.Text.Json.Nodes.JsonNode.Parse(texto)!;

        var esquema = doc["components"]!["securitySchemes"]!["sofiaBearer"]!;
        Assert.Equal("http", esquema["type"]!.GetValue<string>());
        Assert.Equal("bearer", esquema["scheme"]!.GetValue<string>());
        Assert.NotNull(doc["paths"]!["/a2a"]!["post"]!["security"]![0]!["sofiaBearer"]);
        Assert.NotNull(doc["paths"]!["/.well-known/agent-card.json"]!["get"]!["security"]![0]!["sofiaBearer"]);
        Assert.Null(doc["paths"]!["/api/v1/consultas"]!["post"]!["security"]);
        Assert.Contains("message/send", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(AgenteApp.TokenSofia, texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sem_swagger_a_raiz_nao_existe()
    {
        using var app = new AgenteApp(containers);
        using var cliente = app.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/")).StatusCode);
    }
}
