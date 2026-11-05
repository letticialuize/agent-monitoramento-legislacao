using Agente.Application.Comum;
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
    string? ClienteNome);

public sealed record MensagemRequest(string? Texto);

public static class ConsultasEndpoints
{
    // Sem autenticação por enquanto (desvio registrado no ADR 0007): todas as chamadas usam este usuário.
    private static readonly ContextoUsuario UsuarioSemLogin = new("sem-login", [Papeis.Consultor, Papeis.Desenvolvedor]);

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
                req.ClienteNome);

            var resultado = await caso.ExecutarAsync(pedido, UsuarioSemLogin, http.CorrelacaoId(), null, ct);
            return resultado.Sucesso ? Results.Ok(resultado.Valor) : Problemas.Validacao(resultado.Erros);
        })
        .WithName("ConsultarNovidades");

        grupo.MapPost("/conversas/{conversaId}/mensagens", async (string conversaId, MensagemRequest req, HttpContext http, ResponderMensagemConversa caso, CancellationToken ct) =>
        {
            var resultado = await caso.ExecutarAsync(conversaId, req.Texto ?? string.Empty, UsuarioSemLogin, http.CorrelacaoId(), ct);
            return resultado.Sucesso
                ? Results.Ok(new { mensagem = resultado.Valor.Mensagem, envelope = resultado.Valor.Envelope })
                : Problemas.Validacao(resultado.Erros);
        })
        .WithName("ResponderMensagemConversa");

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
