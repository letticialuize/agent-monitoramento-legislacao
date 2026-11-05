using System.Globalization;
using System.Text.Json.Nodes;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Monitoramento;
using Agente.Domain.Comum;
using Api.Agente.Observabilidade;

namespace Api.Agente.Endpoints;

/// <summary>ConsultaRequest do OpenAPI (contracts/api-agente.openapi.yaml).</summary>
public sealed record PeriodoRequest(DateOnly? Inicio, DateOnly? Fim);

public sealed record ConsultaRequest(
    string? Assunto,
    PeriodoRequest? Periodo,
    IReadOnlyList<string>? Esferas,
    IReadOnlyList<string>? Perfis,
    IReadOnlyList<string>? Grupos,
    bool? IncluirPendentes,
    string? ClienteNome,
    bool? IncluirNaoTributarias);

public sealed record MensagemRequest(string? Texto);

public sealed record RespostaConversaDto(string Mensagem, EnvelopeResposta Envelope);

public static class ConsultasEndpoints
{
    // Sem autenticação por enquanto (desvio registrado no ADR 0007): todas as chamadas usam este usuário.
    private static readonly ContextoUsuario UsuarioSemLogin = new("sem-login", [Papeis.Consultor, Papeis.Desenvolvedor]);

    /// <summary>Exemplo válido para o Swagger: os últimos 30 dias (prazo de retenção).</summary>
    public static JsonObject ExemploConsulta(DateOnly hoje) => new()
    {
        ["assunto"] = "nota técnica",
        ["perfis"] = new JsonArray("desenvolvedor"),
        ["periodo"] = new JsonObject
        {
            ["inicio"] = hoje.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["fim"] = hoje.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        },
    };

    public static JsonObject ExemploMensagem() => new() { ["texto"] = "O que houve de novidades ontem?" };

    public static IEndpointRouteBuilder MapConsultas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1");

        // FR-010..013: só chama o caso de uso (Clean Architecture).
        grupo.MapPost("/consultas", async (ConsultaRequest req, HttpContext http, ConsultarNovidades caso, CancellationToken ct) =>
        {
            var esferas = ConsultarNovidades.LerLista<Esfera>(req.Esferas, "esferas");
            var perfis = ConsultarNovidades.LerLista<Perfil>(req.Perfis, "perfis");
            var grupos = ConsultarNovidades.LerLista<Grupo>(req.Grupos, "grupos");
            var erros = esferas.Erros.Concat(perfis.Erros).Concat(grupos.Erros).ToList();
            if (erros.Count > 0)
            {
                return Problemas.Validacao(erros);
            }

            var pedido = new PedidoConsulta(
                req.Assunto ?? string.Empty,
                req.Periodo?.Inicio,
                req.Periodo?.Fim,
                esferas.Valor,
                perfis.Valor,
                grupos.Valor,
                req.IncluirPendentes ?? true,
                req.ClienteNome,
                req.IncluirNaoTributarias ?? false);

            var resultado = await caso.ExecutarAsync(pedido, UsuarioSemLogin, http.CorrelacaoId(), null, ct);
            return resultado.Sucesso ? Results.Ok(resultado.Valor) : Problemas.Validacao(resultado.Erros);
        })
        .WithName("ConsultarNovidades")
        .WithTags("Novidades")
        .WithSummary("Busca estruturada de novidades (filtros em JSON)")
        .WithDescription(
            """
            Lê a base já coletada. Todos os campos são opcionais: sem `assunto`, lista todas as novidades do período; sem `periodo`, usa os últimos 7 dias. A resposta traz a lista e os grupos por perfil, esfera e situação.

            Valores aceitos:
            - `perfis`: consultor_tributario (padrão, se omitido), desenvolvedor
            - `esferas`: federal, estadual, municipal
            - `grupos`: legislacao_federal, legislacao_estadual, legislacao_municipal, tributos, tribunais_judiciais, administrativos_contas

            Novidades sem relação tributária (indício da IA) ficam ocultas e são contadas em `limitacoes`; use `incluirNaoTributarias` para conferir. `clienteNome` ainda não é aceito (Entrega 2).
            """)
        .Produces<EnvelopeResposta>()
        .ProducesValidationProblem();

        grupo.MapPost("/conversas/{conversaId}/mensagens", async (string conversaId, MensagemRequest req, HttpContext http, ResponderMensagemConversa caso, CancellationToken ct) =>
        {
            var resultado = await caso.ExecutarAsync(conversaId, req.Texto ?? string.Empty, UsuarioSemLogin, http.CorrelacaoId(), ct);
            return resultado.Sucesso
                ? Results.Ok(new RespostaConversaDto(resultado.Valor.Mensagem, resultado.Valor.Envelope))
                : Problemas.Validacao(resultado.Erros);
        })
        .WithName("ResponderMensagemConversa")
        .WithTags("Novidades")
        .WithSummary("Pergunta ao agente em linguagem natural")
        .WithDescription("Ex.: O que houve de novidades ontem? O agente interpreta a pergunta e busca na base coletada; perguntas fora do âmbito tributário são recusadas. `conversaId` é um identificador livre: repita o mesmo para continuar a conversa. Exige a chave e o modelo da OpenAI configurados; sem eles, responde como indisponível.")
        .Produces<RespostaConversaDto>()
        .ProducesValidationProblem();

        return app;
    }
}

internal static class Problemas
{
    public static IResult Validacao(IEnumerable<ErroDominio> erros) =>
        Results.ValidationProblem(
            erros.GroupBy(e => e.Codigo).ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray()),
            title: "Erro de validação da entrada");
}
