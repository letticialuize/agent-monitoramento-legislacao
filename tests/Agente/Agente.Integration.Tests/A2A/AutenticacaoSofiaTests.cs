using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Agente.Integration.Tests.Infra;
using Api.Agente.A2A;

namespace Agente.Integration.Tests.A2A;

/// <summary>Chave Bearer da SOFIA no Agent Card e no RPC, e tasks/get e tasks/cancel (feature 006, ADR 0010).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class AutenticacaoSofiaTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public Task InitializeAsync()
    {
        // LLM "configurado" para provar que a recusa acontece antes de qualquer chamada à OpenAI.
        _app = new AgenteApp(containers, llmConfigurado: true);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private static StringContent Json(JsonNode corpo) => new(corpo.ToJsonString(), Encoding.UTF8, "application/json");

    private static JsonNode MessageSend() =>
        A2AServidorTests.MessageSend(A2AServidorTests.Solicitacao(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));

    private static JsonObject Rpc(string metodo, string? idTarefa) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = 9,
        ["method"] = metodo,
        ["params"] = idTarefa is null ? new JsonObject() : new JsonObject { ["id"] = idTarefa },
    };

    public static TheoryData<string?> CredenciaisInvalidas => new()
    {
        null,
        "Bearer token-errado",
        "Bearer " + AgenteApp.TokenSofia.ToUpperInvariant(),
        "Basic " + AgenteApp.TokenSofia,
        AgenteApp.TokenSofia,
    };

    [Theory]
    [MemberData(nameof(CredenciaisInvalidas))]
    public async Task Card_sem_chave_valida_responde_401_com_WWW_Authenticate(string? autorizacao)
    {
        using var cliente = _app.ClienteSofia(autorizacao);

        var resposta = await cliente.GetAsync(AgentCardSofia.Rota);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains(resposta.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.DoesNotContain("protocolVersion", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CredenciaisInvalidas))]
    public async Task RPC_sem_chave_valida_responde_401_sem_chamar_o_LLM(string? autorizacao)
    {
        using var cliente = _app.ClienteSofia(autorizacao);

        var resposta = await cliente.PostAsync(AgentCardSofia.RotaA2A, Json(MessageSend()));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains(resposta.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Empty(_app.OpenAi.LogEntries);
    }

    [Fact]
    public async Task Token_na_URL_nao_e_aceito()
    {
        using var cliente = _app.ClienteSofia(null);

        var resposta = await cliente.GetAsync($"{AgentCardSofia.Rota}?access_token={AgenteApp.TokenSofia}");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Sem_chave_configurada_a_integracao_fica_indisponivel_e_nunca_aberta(string? valor)
    {
        using var app = new AgenteApp(containers, extras: new Dictionary<string, string?> { ["SOFIA_A2A_TOKEN"] = valor });
        using var semCabecalho = app.ClienteSofia(null);
        using var comCabecalhoVazio = app.ClienteSofia("Bearer ");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await semCabecalho.GetAsync(AgentCardSofia.Rota)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await comCabecalhoVazio.PostAsync(AgentCardSofia.RotaA2A, Json(MessageSend()))).StatusCode);
    }

    [Fact]
    public async Task Card_autenticado_declara_Bearer_sem_expor_a_chave()
    {
        using var cliente = _app.ClienteSofia();

        var resposta = await cliente.GetAsync(AgentCardSofia.Rota);
        var json = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(ContratoSofiaA2ATests.ValidaAgentCard(json), json);
        Assert.Equal("bearer", JsonNode.Parse(json)!["securitySchemes"]!["sofiaService"]!["scheme"]!.GetValue<string>());
        Assert.DoesNotContain(AgenteApp.TokenSofia, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tasks_get_devolve_a_mesma_Task_e_tasks_cancel_recusa_Task_finalizada()
    {
        // LLM sem resposta simulada: a análise conclui com status erro, o que basta para o ciclo da Task.
        using var semLlm = new AgenteApp(containers);
        using var cliente = semLlm.ClienteSofia();
        var enviada = JsonNode.Parse(await (await cliente.PostAsync(AgentCardSofia.RotaA2A, Json(MessageSend()))).Content.ReadAsStringAsync())!["result"]!;
        var idTarefa = enviada["id"]!.GetValue<string>();

        var obtida = JsonNode.Parse(await (await cliente.PostAsync(AgentCardSofia.RotaA2A, Json(Rpc("tasks/get", idTarefa)))).Content.ReadAsStringAsync())!;
        var cancelada = JsonNode.Parse(await (await cliente.PostAsync(AgentCardSofia.RotaA2A, Json(Rpc("tasks/cancel", idTarefa)))).Content.ReadAsStringAsync())!;

        Assert.Equal(9, obtida["id"]!.GetValue<int>());
        Assert.Equal(enviada.ToJsonString(), obtida["result"]!.ToJsonString());
        Assert.Equal(A2AEndpoints.TarefaNaoCancelavel, cancelada["error"]!["code"]!.GetValue<int>());

        var logs = string.Join("\n", semLlm.Logs.Linhas);
        Assert.DoesNotContain(AgenteApp.TokenSofia, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("CONTEUDO-DOC", logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tasks/get", "nao-existe", A2AEndpoints.TarefaInexistente)]
    [InlineData("tasks/cancel", "nao-existe", A2AEndpoints.TarefaInexistente)]
    [InlineData("tasks/get", null, A2AEndpoints.ParametrosInvalidos)]
    [InlineData("tasks/resubscribe", "x", A2AEndpoints.MetodoInexistente)]
    public async Task Erros_de_tarefa_seguem_o_JSON_RPC_sem_stack_trace(string metodo, string? idTarefa, int codigo)
    {
        using var cliente = _app.ClienteSofia();

        var corpo = await (await cliente.PostAsync(AgentCardSofia.RotaA2A, Json(Rpc(metodo, idTarefa)))).Content.ReadAsStringAsync();

        Assert.Equal(codigo, JsonNode.Parse(corpo)!["error"]!["code"]!.GetValue<int>());
        Assert.DoesNotContain("   at ", corpo, StringComparison.Ordinal);
    }
}
