using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Agente.Infrastructure.Persistencia;
using Agente.Integration.Tests.Infra;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Monitoramento;

/// <summary>Entrega 1 — consulta estruturada sobre a base coletada (quickstart cenários 1, 2, 3, 5 e FR-012).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ConsultaTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public async Task InitializeAsync()
    {
        _app = new AgenteApp(containers);
        Simuladores.Fontes(_app.Fontes);
        _app.CreateClient().Dispose();
        await _app.ColetarAsync();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private async Task<JsonNode> Consultar(object corpo)
    {
        using var cliente = _app.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/v1/consultas", corpo);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, texto);
        return JsonNode.Parse(texto)!;
    }

    private static JsonArray Novidades(JsonNode env) => env["resultado"]!["novidades"]!.AsArray();

    [Fact]
    public async Task Cenario1_ato_de_fonte_oficial_confirmado_com_evidencia_principal()
    {
        // O DOU (INLABS) está bloqueado; o cenário usa a fonte oficial do Senado (research R-03).
        var env = await Consultar(new { assunto = "crédito rural" });
        var n = Assert.Single(Novidades(env));
        Assert.Equal("confirmada", n!["situacao"]!.GetValue<string>());
        Assert.Equal("federal", n["esfera"]!.GetValue<string>());
        Assert.Equal("2026-10-28", n["dataPublicacao"]!.GetValue<string>());
        Assert.Contains("consultor_tributario", n["perfis"]!.AsArray().Select(p => p!.GetValue<string>()));
        var principal = n["evidencias"]!.AsArray().Single(e => e!["principal"]!.GetValue<bool>());
        Assert.Equal(Simuladores.UrlSenado(_app.Fontes), principal!["url"]!.GetValue<string>());
        Assert.Equal("T-SENADO", principal["fonteCodigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cenario2_sinonimo_CBS_encontra_ementa_por_extenso()
    {
        var env = await Consultar(new { assunto = "CBS", incluirPendentes = false });
        var n = Assert.Single(Novidades(env));
        Assert.Equal(Simuladores.EmentaCamara, n!["ementa"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cenario3_fonte_fora_do_ar_e_conector_bloqueado_viram_falha_tecnica()
    {
        var env = await Consultar(new { assunto = "CBS" });
        Assert.Equal("concluido_com_limitacoes", env["status"]!.GetValue<string>());
        var falhas = env["resultado"]!["fontesComFalha"]!.AsArray().Select(f => f!.GetValue<string>()).ToList();
        Assert.Contains("T-FALHA", falhas);
        Assert.Contains("T-INLABS", falhas);
        Assert.Contains(env["achados"]!.AsArray(), a => a!["categoria"]!.GetValue<string>() == "falha_tecnica" && a["referencia"]?.GetValue<string>() == "T-FALHA");
    }

    [Fact]
    public async Task Cenario5_agregador_fica_pendente_e_fonte_fora_do_catalogo_e_nao_catalogada()
    {
        var env = await Consultar(new { assunto = "ISS" });
        var qd = Assert.Single(Novidades(env));
        Assert.Equal("pendente_confirmacao", qd!["situacao"]!.GetValue<string>());
        Assert.Equal("municipal", qd["esfera"]!.GetValue<string>());
        Assert.True(qd["requerRevisaoHumana"]!.GetValue<bool>());

        var cbs = await Consultar(new { assunto = "Portaria" });
        var pagina = Assert.Single(Novidades(cbs));
        Assert.Equal("fonte_nao_catalogada", pagina!["situacao"]!.GetValue<string>());
        Assert.True(cbs["requerRevisaoHumana"]!.GetValue<bool>());
        Assert.Equal("123", pagina["numeroAto"]!.GetValue<string>());

        var soConfirmadas = await Consultar(new { assunto = "ISS", incluirPendentes = false });
        Assert.Empty(Novidades(soConfirmadas));
    }

    [Fact]
    public async Task Sem_novidades_informa_fontes_e_nao_afirma_ausencia_de_mudanca()
    {
        var env = await Consultar(new { assunto = "assunto inexistente xyz" });
        Assert.Empty(Novidades(env));
        Assert.StartsWith("Sem novidades encontradas", env["resultado"]!["mensagemSemNovidades"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotEmpty(env["resultado"]!["fontesConsultadas"]!.AsArray());
        Assert.Contains(env["achados"]!.AsArray(), a => a!["categoria"]!.GetValue<string>() == "ausencia_evidencia");
    }

    [Fact]
    public async Task Resposta_agrupa_por_perfil_esfera_e_situacao()
    {
        var env = await Consultar(new { assunto = "CBS" });

        var perfil = Assert.Single(env["resultado"]!["grupos"]!.AsArray());
        Assert.Equal("consultor_tributario", perfil!["perfil"]!.GetValue<string>());
        Assert.Equal(2, perfil["total"]!.GetValue<int>());
        var esfera = Assert.Single(perfil["esferas"]!.AsArray());
        Assert.Equal("federal", esfera!["esfera"]!.GetValue<string>());
        var situacoes = esfera["situacoes"]!.AsArray();
        Assert.Equal(["confirmada", "fonte_nao_catalogada"], situacoes.Select(s => s!["situacao"]!.GetValue<string>()));
        Assert.Equal(Simuladores.EmentaCamara, situacoes[0]!["novidades"]![0]!["ementa"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sem_assunto_lista_todas_as_novidades_do_periodo()
    {
        var doDia = await Consultar(new { periodo = new { inicio = "2026-10-28", fim = "2026-10-28" } });
        Assert.Equal("(todas as novidades)", doDia["resultado"]!["assunto"]!.GetValue<string>());
        Assert.Equal(3, Novidades(doDia).Count);
        Assert.All(Novidades(doDia), n => Assert.Equal("2026-10-28", n!["dataPublicacao"]!.GetValue<string>()));

        var padrao = await Consultar(new { });
        Assert.Equal(4, Novidades(padrao).Count);
    }

    [Fact]
    public async Task Novidade_sem_relacao_tributaria_fica_oculta_e_a_quantidade_e_informada()
    {
        using (var escopo = _app.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AgenteDbContext>();
            var novidade = await db.Novidades.SingleAsync(n => n.Ementa == Simuladores.EmentaCamara);
            Assert.True(novidade.AplicarSugestaoIa(novidade.Perfis, null, null, _app.Tempo.GetUtcNow(), relacionadaTributos: false).Sucesso);
            await db.SaveChangesAsync();
        }

        var padrao = await Consultar(new { assunto = "CBS", incluirPendentes = false });
        Assert.Empty(Novidades(padrao));
        Assert.Contains(padrao["limitacoes"]!.AsArray(), l => l!.GetValue<string>().StartsWith("1 novidade(s) ocultada(s)", StringComparison.Ordinal));

        var conferencia = await Consultar(new { assunto = "CBS", incluirPendentes = false, incluirNaoTributarias = true });
        var oculta = Assert.Single(Novidades(conferencia));
        Assert.False(oculta!["relacionadaTributos"]!.GetValue<bool>());
        Assert.DoesNotContain(conferencia["limitacoes"]!.AsArray(), l => l!.GetValue<string>().Contains("ocultada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sem_novidades_grupos_vem_vazio()
    {
        var env = await Consultar(new { assunto = "assunto inexistente xyz" });
        Assert.Empty(env["resultado"]!["grupos"]!.AsArray());
    }
    [Fact]
    public async Task Filtro_por_perfil_e_esfera()
    {
        var dev = await Consultar(new { assunto = "CBS", perfis = new[] { "desenvolvedor" } });
        Assert.Empty(Novidades(dev));
        var municipal = await Consultar(new { assunto = "ISS", esferas = new[] { "municipal" } });
        Assert.Single(Novidades(municipal));
    }

    [Fact]
    public async Task Resposta_valida_contra_o_schema_do_envelope_e_registra_execucao()
    {
        using var cliente = _app.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS" });
        var texto = await resposta.Content.ReadAsStringAsync();
        var schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "Contratos", "envelope-resposta.schema.json"));
        var avaliacao = schema.Evaluate(JsonDocument.Parse(texto).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(avaliacao.IsValid, texto);
        Assert.True(resposta.Headers.Contains("X-Correlation-Id"));

        var id = Guid.Parse(JsonNode.Parse(texto)!["execucaoId"]!.GetValue<string>());
        var consulta = await _app.NoBanco(db => db.Consultas.Include(c => c.Etapas).SingleAsync(c => c.Id == id));
        Assert.Equal(StatusProcessamento.ConcluidoComLimitacoes, consulta.Status);
        Assert.Contains(consulta.Etapas, e => e.Etapa == EtapaFluxo.Coleta);
        Assert.Contains(consulta.Etapas, e => e.Etapa == EtapaFluxo.Resposta);
    }

    [Fact]
    public async Task Contrato_recusa_campos_extras_valores_invalidos_e_cliente()
    {
        using var cliente = _app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS", cnpj = "00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "C" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS", esferas = new[] { "galactica" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS", clienteNome = "Farmácia X" })).StatusCode);
    }

    [Fact]
    public async Task Consulta_nao_exige_autenticacao_enquanto_vale_o_adr_0007()
    {
        using var cliente = _app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS" })).StatusCode);
    }

    [Fact]
    public async Task Coleta_repetida_nao_duplica_novidades()
    {
        var antes = await _app.NoBanco(db => db.Novidades.CountAsync());
        _app.Tempo.Advance(TimeSpan.FromDays(1));
        await _app.ColetarAsync();
        var depois = await _app.NoBanco(db => db.Novidades.CountAsync());
        Assert.Equal(antes, depois);
        Assert.Equal(4, depois);
    }
}

/// <summary>Corte por limite de itens (FR-011).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ConsultaLimiteTests(ContainersFixture containers) : IAsyncLifetime
{
    private AgenteApp _app = null!;

    public async Task InitializeAsync()
    {
        _app = new AgenteApp(containers, extras: new() { ["Consulta:LimiteItens"] = "1" });
        Simuladores.Fontes(_app.Fontes);
        _app.CreateClient().Dispose();
        await _app.ColetarAsync();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Assunto_amplo_informa_itens_omitidos()
    {
        using var cliente = _app.CreateClient();
        var env = JsonNode.Parse(await (await cliente.PostAsJsonAsync("/api/v1/consultas", new { assunto = "CBS" })).Content.ReadAsStringAsync())!;
        Assert.Single(env["resultado"]!["novidades"]!.AsArray());
        Assert.Equal(1, env["resultado"]!["itensOmitidos"]!.GetValue<int>());
        Assert.Contains(env["limitacoes"]!.AsArray(), l => l!.GetValue<string>().Contains("omitida", StringComparison.Ordinal));
    }
}
