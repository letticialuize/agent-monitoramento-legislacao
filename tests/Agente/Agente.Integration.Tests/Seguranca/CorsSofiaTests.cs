using System.Net;
using System.Text;
using Agente.Integration.Tests.Infra;
using Api.Agente.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Seguranca;

/// <summary>Política SofiaBrowser isolada, sem bancos: roda mesmo sem Docker (feature 003).</summary>
public sealed class CorsSofiaPoliticaTests
{
    private const string Permitida = "http://localhost:4200";

    [Theory]
    [InlineData("*")]
    [InlineData("https://sofia-infis.onrender.com/")]
    [InlineData("https://sofia-infis.onrender.com/app")]
    [InlineData("https://*.onrender.com")]
    [InlineData("sofia-infis.onrender.com")]
    [InlineData("ftp://sofia-infis.onrender.com")]
    public void Origem_invalida_impede_a_subida(string origem)
    {
        var config = Config(origem);

        Assert.Throws<InvalidOperationException>(() => CorsSofia.LerOrigens(config));
    }

    [Fact]
    public void Origens_validas_sao_aceitas_sem_duplicar()
    {
        var origens = CorsSofia.LerOrigens(Config(Permitida, " https://sofia-infis.onrender.com ", "", Permitida));

        Assert.Equal([Permitida, "https://sofia-infis.onrender.com"], origens);
    }

    [Fact]
    public async Task Preflight_de_origem_permitida_recebe_permissao()
    {
        await using var app = await Iniciar(Permitida);

        var resposta = await Preflight(app, "/rota", Permitida);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(Permitida, Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Cabecalho(resposta, "Access-Control-Allow-Methods"));
        Assert.Contains("content-type", Cabecalho(resposta, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_de_origem_desconhecida_nao_recebe_permissao()
    {
        await using var app = await Iniciar(Permitida);

        var resposta = await Preflight(app, "/rota", "https://site-nao-autorizado.example");

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Sem_origens_configuradas_ninguem_recebe_permissao()
    {
        await using var app = await Iniciar();

        var resposta = await Preflight(app, "/rota", Permitida);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Rota_sem_RequireCorsSofia_nao_recebe_permissao()
    {
        await using var app = await Iniciar(Permitida);

        var resposta = await Preflight(app, "/fora", Permitida);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static IConfiguration Config(params string[] origens) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(origens.Select((o, i) => new KeyValuePair<string, string?>($"{CorsSofia.Secao}:{i}", o)))
            .Build();

    private static async Task<WebApplication> Iniciar(params string[] origens)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddCorsSofia(Config(origens));
        var app = builder.Build();
        app.UseCors();
        app.MapPost("/rota", () => Results.Ok()).RequireCorsSofia();
        app.MapPost("/fora", () => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> Preflight(WebApplication app, string rota, string origem)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Options, rota);
        pedido.Headers.Add("Origin", origem);
        pedido.Headers.Add("Access-Control-Request-Method", "POST");
        pedido.Headers.Add("Access-Control-Request-Headers", "content-type");
        return app.GetTestClient().SendAsync(pedido);
    }

    internal static string Cabecalho(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : string.Empty;
}

/// <summary>CORS na Api.Agente completa: rotas reais, pipeline real e chamadas sem Origin (feature 003).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class CorsSofiaApiTests(ContainersFixture containers) : IAsyncLifetime
{
    private const string Local = "http://localhost:4200";
    private const string Producao = "https://sofia-infis.onrender.com";
    private const string Desconhecida = "https://site-nao-autorizado.example";
    private const string RotaConsultas = "/api/v1/consultas";
    private const string RotaConversa = "/api/v1/conversas/conversa-cors/mensagens";

    private AgenteApp _app = null!;

    public Task InitializeAsync()
    {
        _app = new AgenteApp(containers, extras: new Dictionary<string, string?>
        {
            [$"{CorsSofia.Secao}:0"] = Local,
            [$"{CorsSofia.Secao}:1"] = Producao,
            ["Agendamento:TokenGatilho"] = "token-de-teste-NAO-USAR",
        });
        Simuladores.Fontes(_app.Fontes);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(RotaConsultas, Local)]
    [InlineData(RotaConsultas, Producao)]
    [InlineData(RotaConversa, Local)]
    public async Task Preflight_de_origem_permitida_recebe_permissao(string rota, string origem)
    {
        var resposta = await Preflight(rota, origem);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(origem, CorsSofiaPoliticaTests.Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", CorsSofiaPoliticaTests.Cabecalho(resposta, "Access-Control-Allow-Methods"));
        Assert.Contains("content-type", CorsSofiaPoliticaTests.Cabecalho(resposta, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData(RotaConsultas, Desconhecida)]
    [InlineData(RotaConversa, Desconhecida)]
    [InlineData("/api/v1/tarefas/coleta", Local)]
    [InlineData("/saude", Local)]
    public async Task Origem_ou_rota_fora_da_politica_nao_recebe_permissao(string rota, string origem)
    {
        var resposta = await Preflight(rota, origem);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Chamada_backend_sem_Origin_funciona_sem_cabecalhos_CORS()
    {
        using var cliente = _app.CreateClient();

        var resposta = await cliente.PostAsync(RotaConsultas, Json("{}"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Chamada_do_navegador_permitido_recebe_origem_e_expoe_a_correlacao()
    {
        using var cliente = _app.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, RotaConsultas) { Content = Json("{}") };
        pedido.Headers.Add("Origin", Producao);

        var resposta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(Producao, CorsSofiaPoliticaTests.Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("X-Correlation-Id", CorsSofiaPoliticaTests.Cabecalho(resposta, "Access-Control-Expose-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Origin", CorsSofiaPoliticaTests.Cabecalho(resposta, "Vary"));
    }

    private async Task<HttpResponseMessage> Preflight(string rota, string origem)
    {
        using var cliente = _app.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Options, rota);
        pedido.Headers.Add("Origin", origem);
        pedido.Headers.Add("Access-Control-Request-Method", "POST");
        pedido.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await cliente.SendAsync(pedido);
    }

    private static StringContent Json(string corpo) => new(corpo, Encoding.UTF8, "application/json");
}
