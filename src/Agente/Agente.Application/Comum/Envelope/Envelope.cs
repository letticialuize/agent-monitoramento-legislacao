using System.Text.Json.Serialization;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;

namespace Agente.Application.Comum.Envelope;

// Espelha specs/001-monitoramento-boletim-diario/contracts/envelope-resposta.schema.json (v1).
// Enums são serializados em snake_case pela Api (ex.: ConcluidoComLimitacoes → "concluido_com_limitacoes").

public sealed record EnvelopeResposta(
    string VersaoContrato,
    Guid ExecucaoId,
    string CorrelacaoId,
    StatusProcessamento Status,
    DateTimeOffset IniciadoEm,
    DateTimeOffset FinalizadoEm,
    bool RequerRevisaoHumana,
    ResultadoConsulta? Resultado,
    IReadOnlyList<Achado> Achados,
    IReadOnlyList<string> Limitacoes,
    ResumoConsumo Consumo)
{
    public const string VersaoAtual = "1.0";
}

public sealed record ResultadoConsulta(
    string Assunto,
    PeriodoDto Periodo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ClienteDto? Cliente,
    IReadOnlyList<string> FontesConsultadas,
    IReadOnlyList<string> FontesComFalha,
    IReadOnlyList<NovidadeDto> Novidades,
    int ItensOmitidos,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MensagemSemNovidades);

public sealed record PeriodoDto(DateOnly Inicio, DateOnly Fim);

public sealed record ClienteDto(string Nome, string Nicho);

public sealed record NovidadeDto(
    Guid Id,
    SituacaoNovidade Situacao,
    Esfera Esfera,
    string? Ente,
    IReadOnlyList<Perfil> Perfis,
    Origem OrigemPerfil,
    DateOnly DataPublicacao,
    string OrgaoEmissor,
    string TipoAto,
    string? NumeroAto,
    string Ementa,
    string? Tema,
    Impacto? Impacto,
    Origem? OrigemImpacto,
    RelacaoNichoDto? RelacaoNicho,
    Guid? RetificaNovidadeId,
    IReadOnlyList<EvidenciaDto> Evidencias,
    bool RequerRevisaoHumana);

public sealed record RelacaoNichoDto(string Justificativa, Origem Origem);

public sealed record EvidenciaDto(string? FonteCodigo, string Url, bool Principal);

public sealed record Achado(CategoriaAchado Categoria, string Descricao, string? Referencia)
{
    /// <summary>Escala pendente (DP-07): sempre nula na v1.</summary>
    public object? Severidade => null;
}

public sealed record ResumoConsumo(int ChamadasLlm, int? TokensEntrada, int? TokensSaida, bool UsoConhecido, decimal? CustoEstimado, string? Moeda)
{
    public static ResumoConsumo Nenhum { get; } = new(0, 0, 0, true, 0m, null);
}
