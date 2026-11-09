using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Agente.Application.Portas;
using Agente.Integration.Tests.Infra;
using Api.Agente.Agendamento;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Monitoramento;

/// <summary>Gatilhos protegidos de coleta e retenção (feature 002, FR-007, FR-008, FR-011; ADR 0008).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class TarefasTests(ContainersFixture containers) : IAsyncLifetime
{
    private const string Token = "token-de-teste-NAO-USAR";
    private const string RotaColeta = "/api/v1/tarefas/coleta";
    private const string RotaRetencao = "/api/v1/tarefas/retencao";

    private AgenteApp _app = null!;

    public Task InitializeAsync()
    {
        _app = new AgenteApp(containers, extras: new Dictionary<string, string?> { ["Agendamento:TokenGatilho"] = Token });
        Simuladores.Fontes(_app.Fontes);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Cliente(string? token = Token)
    {
        var cliente = _app.CreateClient();
        if (token is not null)
        {
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return cliente;
    }

    [Theory]
    [InlineData(RotaColeta)]
    [InlineData(RotaRetencao)]
    public async Task Sem_token_configurado_as_rotas_nao_existem(string rota)
    {
        using var app = new AgenteApp(containers);
        using var cliente = app.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        var resposta = await cliente.PostAsync(rota, null);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Theory]
    [InlineData(RotaColeta, null)]
    [InlineData(RotaColeta, "token-errado")]
    [InlineData(RotaRetencao, null)]
    [InlineData(RotaRetencao, "token-errado")]
    public async Task Sem_credencial_valida_recusa_e_nao_executa(string rota, string? token)
    {
        using var cliente = Cliente(token);

        var resposta = await cliente.PostAsync(rota, null);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(0, await _app.NoBanco(db => db.ExecucoesColeta.CountAsync()));
    }

    [Fact]
    public async Task Com_token_valido_executa_a_coleta_sem_registrar_o_token_nos_logs()
    {
        using var cliente = Cliente();

        var resposta = await cliente.PostAsync(RotaColeta, null);

        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, texto);
        var corpo = JsonNode.Parse(texto)!;
        Assert.True(corpo["fontes"]!.GetValue<int>() > 0);
        Assert.True(corpo["novasNovidades"]!.GetValue<int>() > 0);
        Assert.False(string.IsNullOrWhiteSpace(corpo["correlacaoId"]!.GetValue<string>()));
        Assert.True(await _app.NoBanco(db => db.ExecucoesColeta.CountAsync()) > 0);
        Assert.DoesNotContain(_app.Logs.Linhas, l => l.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Com_token_valido_aplica_a_retencao()
    {
        using var cliente = Cliente();

        var resposta = await cliente.PostAsync(RotaRetencao, null);

        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, texto);
        Assert.NotNull(JsonNode.Parse(texto)!["removidosPorTabela"]);
    }

    [Theory]
    [InlineData(RotaColeta, AgendadorColeta.NomeBloqueio)]
    [InlineData(RotaRetencao, AgendadorRetencao.NomeBloqueio)]
    public async Task Com_execucao_em_andamento_responde_conflito(string rota, string bloqueio)
    {
        using var cliente = Cliente();
        var trava = await _app.Services.GetRequiredService<IBloqueioExecucao>().TentarAdquirirAsync(bloqueio, CancellationToken.None);
        Assert.NotNull(trava);

        try
        {
            var resposta = await cliente.PostAsync(rota, null);

            Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        }
        finally
        {
            await trava.DisposeAsync();
        }
    }
}
