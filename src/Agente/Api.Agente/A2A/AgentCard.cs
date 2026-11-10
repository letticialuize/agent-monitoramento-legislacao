using Agente.Application.Sofia;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A;

// Agent Card no perfil A2A v0.3. Publicado em /.well-known/agent-card.json para a SOFIA descobrir o agente.

public sealed record AgentCard(
    string ProtocolVersion,
    string Name,
    string Description,
    string Url,
    string PreferredTransport,
    string Version,
    CapacidadesAgente Capabilities,
    IReadOnlyList<string> DefaultInputModes,
    IReadOnlyList<string> DefaultOutputModes,
    IReadOnlyList<HabilidadeAgente> Skills,
    IReadOnlyDictionary<string, EsquemaSegurancaA2A> SecuritySchemes,
    IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<string>>> Security);

public sealed record CapacidadesAgente(bool Streaming, bool PushNotifications);

/// <summary>Esquema HTTP Bearer declarado no card. O segredo nunca aparece aqui.</summary>
public sealed record EsquemaSegurancaA2A(string Type, string Scheme);

public sealed record HabilidadeAgente(string Id, string Name, string Description, IReadOnlyList<string> Tags, IReadOnlyList<string>? Examples);

/// <summary>Seção <c>A2A</c> da configuração.</summary>
public sealed class OpcoesA2A
{
    /// <summary>URL pública do endpoint A2A (ex.: https://agente.vercel.app/a2a). Vazia: deriva do pedido.</summary>
    public string? UrlPublica { get; set; }

    public string VersaoAgente { get; set; } = "1.0.0";

    /// <summary>Tempo máximo de uma análise em <c>message/send</c>; ao estourar, a Task volta <c>failed</c>.</summary>
    public int TimeoutSegundos { get; set; } = 120;
}

public static class AgentCardSofia
{
    public const string Rota = "/.well-known/agent-card.json";
    public const string RotaA2A = "/a2a";
    public const string IdHabilidade = "monitorar_legislacao_tributaria";
    public const string IdHabilidadeDocumentos = "analisar_documentos";
    public const string EsquemaSeguranca = "sofiaService";

    public static AgentCard Criar(OpcoesContratoSofia contrato, OpcoesA2A a2a, string urlA2A) => new(
        ProtocoloA2A.Versao,
        contrato.NomeAgente,
        "Monitora publicações oficiais de legislação e jurisprudência tributária (federal, estadual e municipal) e responde a perguntas da SOFIA com as novidades coletadas e suas fontes oficiais.",
        urlA2A,
        ProtocoloA2A.Transporte,
        a2a.VersaoAgente,
        new CapacidadesAgente(Streaming: false, PushNotifications: false),
        ["text/plain"],
        ["text/plain"],
        [
            new HabilidadeAgente(
                IdHabilidade,
                "Novidades da legislação e jurisprudência tributária",
                $"Recebe SolicitacaoAnaliseSofia (contrato SOFIA v{ContratoSofia.Versao}) em TextPart JSON e devolve ResultadoAnaliseSofia no artefato {ContratoSofia.NomeArtefato}, com as novidades encontradas e os links oficiais como evidência. Id do agente: {contrato.IdAgente}.",
                ["infis", "sofia", "tributario", "legislacao", "jurisprudencia", $"contrato-sofia-{ContratoSofia.Versao}"],
                ["Quais novidades de ICMS saíram nesta semana?", "Houve alguma decisão do STF sobre PIS/COFINS ontem?"]),
            new HabilidadeAgente(
                IdHabilidadeDocumentos,
                "Análise documental",
                "Lê os documentos textuais enviados pela SOFIA como contexto da pergunta e indica as novidades tributárias oficiais relacionadas. Não valida o conteúdo dos documentos nem cita trechos deles como evidência.",
                ["infis", "fiscal", "sofia"],
                null),
        ],
        new Dictionary<string, EsquemaSegurancaA2A> { [EsquemaSeguranca] = new("http", "bearer") },
        [new Dictionary<string, IReadOnlyList<string>> { [EsquemaSeguranca] = [] }]);

    /// <summary>Registra opções, validador, mapeador e confere a URL pública na subida.</summary>
    public static IServiceCollection AddContratoSofiaA2A(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<OpcoesContratoSofia>(configuracao.GetSection("Sofia"));
        services.Configure<OpcoesA2A>(configuracao.GetSection("A2A"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpcoesContratoSofia>>().Value);
        services.AddSingleton<ValidadorContratoSofia>();
        services.AddSingleton<MapeadorA2ASofia>();

        var url = configuracao["A2A:UrlPublica"];
        if (!string.IsNullOrWhiteSpace(url)
            && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException("A2A:UrlPublica deve ser uma URL http(s) absoluta do endpoint A2A.");
        }

        return services;
    }

    public static IEndpointRouteBuilder MapAgentCard(this IEndpointRouteBuilder app)
    {
        // Protegido pela chave da SOFIA (política do treinamento, ADR 0010). O card declara o esquema, nunca o segredo.
        app.MapGet(Rota, (HttpRequest req, IOptions<OpcoesContratoSofia> contrato, IOptions<OpcoesA2A> a2a) =>
        {
            var url = string.IsNullOrWhiteSpace(a2a.Value.UrlPublica)
                ? $"{req.Scheme}://{req.Host}{req.PathBase}{RotaA2A}"
                : a2a.Value.UrlPublica;
            return Results.Json(Criar(contrato.Value, a2a.Value, url), ProtocoloA2A.Json);
        })
        .WithName("AgentCard")
        .WithTags("A2A")
        .WithSummary("Agent Card A2A v0.3 para descoberta pela SOFIA (Bearer SOFIA_A2A_TOKEN)")
        .RequireTokenSofia()
        .Produces<AgentCard>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
