using System.Text.Json;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Json.Schema;

namespace Agente.Contract.Tests;

/// <summary>O envelope serializado pela API DEVE validar contra contracts/envelope-resposta.schema.json (v1).</summary>
public class EnvelopeContractTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "Contratos", "envelope-resposta.schema.json"));

    internal static EvaluationResults Validar(object envelope)
    {
        var json = JsonSerializer.Serialize(envelope, JsonContrato.Opcoes);
        return Schema.Evaluate(JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    internal static string Erros(EvaluationResults r) =>
        string.Join("\n", (r.Details ?? []).Where(d => d.Errors is { Count: > 0 }).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")));

    internal static NovidadeDto Novidade(SituacaoNovidade situacao = SituacaoNovidade.Confirmada, Impacto? impacto = null) =>
        new(Guid.NewGuid(), situacao, Esfera.Estadual, "SP", [Perfil.ConsultorTributario, Perfil.Desenvolvedor], Origem.IndicioIa,
            new DateOnly(2026, 10, 28), "TJSP", "Acórdão", null, "Ementa", "ICMS", impacto, impacto is null ? null : Origem.IndicioIa,
            null, null, [new EvidenciaDto(null, "https://www.tjsp.jus.br/x", true)], true);

    private static EnvelopeResposta Envelope(ResultadoConsulta? resultado, ResumoConsumo? consumo = null) =>
        new(EnvelopeResposta.VersaoAtual, Guid.NewGuid(), "c-1", StatusProcessamento.ConcluidoComLimitacoes,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, resultado,
            [new Achado(CategoriaAchado.FalhaTecnica, "Fonte fora do ar", "FED-DOU"), new Achado(CategoriaAchado.IndicioIa, "x", null)],
            ["limitação"], consumo ?? ResumoConsumo.Nenhum);

    [Fact]
    public void Envelope_completo_e_valido()
    {
        NovidadeDto[] novidades = [Novidade(), Novidade(SituacaoNovidade.PendenteConfirmacao, Impacto.Alto)];
        var resultado = new ResultadoConsulta("ICMS", new PeriodoDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 29)), null,
            ["FED-DOU"], ["FED-DOU"], novidades, 3, null, AgrupadorNovidades.Agrupar(novidades, []));
        var r = Validar(Envelope(resultado));
        Assert.True(r.IsValid, Erros(r));
    }

    [Fact]
    public void Envelope_sem_resultado_e_sem_novidades_e_valido()
    {
        Assert.True(Validar(Envelope(null)).IsValid);
        var vazio = new ResultadoConsulta("x", new PeriodoDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)), null, [], [], [], 0, "Sem novidades", []);
        var r = Validar(Envelope(vazio));
        Assert.True(r.IsValid, Erros(r));
    }

    [Fact]
    public void Consumo_desconhecido_e_representado_como_nulo()
    {
        var r = Validar(Envelope(null, new ResumoConsumo(2, null, null, false, null, null)));
        Assert.True(r.IsValid, Erros(r));
    }

    [Fact]
    public void Enums_seguem_os_nomes_tecnicos_do_contrato()
    {
        var json = JsonSerializer.Serialize(Envelope(null), JsonContrato.Opcoes);
        Assert.Contains("\"concluido_com_limitacoes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"falha_tecnica\"", json, StringComparison.Ordinal);
        Assert.Contains("\"indicio_ia\"", json, StringComparison.Ordinal);
        Assert.Contains("\"severidade\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Novidade_sem_evidencia_viola_o_contrato()
    {
        var n = Novidade() with { Evidencias = [] };
        var resultado = new ResultadoConsulta("x", new PeriodoDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)), null, [], [], [n], 0, null, []);
        Assert.False(Validar(Envelope(resultado)).IsValid);
    }
}
