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
    public const string VersaoAtual = "1.2";
}

public sealed record ResultadoConsulta(
    string Assunto,
    PeriodoDto Periodo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ClienteDto? Cliente,
    IReadOnlyList<string> FontesConsultadas,
    IReadOnlyList<string> FontesComFalha,
    IReadOnlyList<NovidadeDto> Novidades,
    int ItensOmitidos,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MensagemSemNovidades,
    IReadOnlyList<GrupoPerfil> Grupos,
    int OcultadasNaoTributarias = 0);

/// <summary>Novidades de um perfil. Uma novidade com dois perfis aparece nos dois grupos (FR-008, FR-011).</summary>
public sealed record GrupoPerfil(Perfil Perfil, int Total, IReadOnlyList<GrupoEsfera> Esferas);

public sealed record GrupoEsfera(Esfera Esfera, int Total, IReadOnlyList<GrupoSituacao> Situacoes);

public sealed record GrupoSituacao(SituacaoNovidade Situacao, IReadOnlyList<NovidadeDto> Novidades);

public static class AgrupadorNovidades
{
    /// <summary>Agrupa por perfil, esfera e situação; com filtro de perfil, só os perfis pedidos aparecem.</summary>
    public static IReadOnlyList<GrupoPerfil> Agrupar(IReadOnlyList<NovidadeDto> novidades, IReadOnlyList<Perfil> filtroPerfis) =>
        Enum.GetValues<Perfil>()
            .Where(p => filtroPerfis.Count == 0 || filtroPerfis.Contains(p))
            .Select(p => (Perfil: p, Itens: novidades.Where(n => n.Perfis.Contains(p)).ToList()))
            .Where(g => g.Itens.Count > 0)
            .Select(g => new GrupoPerfil(g.Perfil, g.Itens.Count, PorEsfera(g.Itens)))
            .ToList();

    private static List<GrupoEsfera> PorEsfera(List<NovidadeDto> itens) =>
        itens.GroupBy(n => n.Esfera)
            .OrderBy(g => g.Key)
            .Select(g => new GrupoEsfera(
                g.Key,
                g.Count(),
                g.GroupBy(n => n.Situacao)
                    .OrderBy(s => s.Key)
                    .Select(s => new GrupoSituacao(s.Key, s.ToList()))
                    .ToList()))
            .ToList();
}

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
    bool RequerRevisaoHumana,
    bool? RelacionadaTributos = null);

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
