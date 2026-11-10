using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Agente.Integration.Tests.Operacao;

/// <summary>
/// Dockerfile.vercel liga ASPNETCORE_FORWARDEDHEADERS_ENABLED: atrás do proxy HTTPS da Vercel, a API
/// precisa enxergar https (Swagger e Agent Card). Sem bancos: roda sem Docker.
/// </summary>
public sealed class CabecalhosEncaminhadosTests
{
    [Theory]
    [InlineData("true", "https")]
    [InlineData(null, "http")]
    public async Task Esquema_vem_do_X_Forwarded_Proto_so_com_a_opcao_ligada(string? habilitado, string esperado)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (habilitado is not null)
        {
            // Mesmo efeito da variável de ambiente ASPNETCORE_FORWARDEDHEADERS_ENABLED.
            builder.WebHost.UseSetting("FORWARDEDHEADERS_ENABLED", habilitado);
        }

        await using var app = builder.Build();
        app.MapGet("/esquema", (HttpRequest r) => r.Scheme);
        await app.StartAsync();

        var contexto = await app.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Scheme = "http";
            c.Request.Path = "/esquema";
            c.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        });

        Assert.Equal(esperado, await new StreamReader(contexto.Response.Body).ReadToEndAsync());
    }
}
