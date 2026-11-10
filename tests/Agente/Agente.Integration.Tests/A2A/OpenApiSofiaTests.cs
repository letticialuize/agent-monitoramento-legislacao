using System.Text.Json.Nodes;
using Api.Agente.A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.A2A;

/// <summary>Bearer da SOFIA e exemplo de message/send no OpenAPI, sem bancos: roda sem Docker.</summary>
public sealed class OpenApiSofiaTests
{
    [Fact]
    public async Task Bearer_so_nas_rotas_protegidas_e_exemplo_de_message_send()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi(o => o.AddSofiaA2A());
        await using var app = builder.Build();
        app.MapOpenApi();
        app.MapPost(AgentCardSofia.RotaA2A, (RequisicaoJsonRpc _) => Results.Ok()).RequireTokenSofia();
        app.MapPost("/aberta", () => Results.Ok());
        await app.StartAsync();

        var texto = await app.GetTestClient().GetStringAsync("/openapi/v1.json");
        var doc = JsonNode.Parse(texto)!;

        Assert.Equal("bearer", doc["components"]!["securitySchemes"]![OpenApiSofia.Esquema]!["scheme"]!.GetValue<string>());
        Assert.NotNull(doc["paths"]!["/a2a"]!["post"]!["security"]![0]![OpenApiSofia.Esquema]);
        Assert.Null(doc["paths"]!["/aberta"]!["post"]!["security"]);
        Assert.Contains("message/send", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Exemplo_de_message_send_passa_no_contrato()
    {
        var rpc = System.Text.Json.JsonSerializer.Deserialize<RequisicaoJsonRpc>(OpenApiSofia.ExemploMessageSend().ToJsonString(), ProtocoloA2A.Json)!;
        var mapeador = new MapeadorA2ASofia(new Agente.Application.Sofia.ValidadorContratoSofia(new Agente.Application.Sofia.OpcoesContratoSofia()));

        var r = mapeador.LerSolicitacao(rpc.Params!.Message);

        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
    }
}
