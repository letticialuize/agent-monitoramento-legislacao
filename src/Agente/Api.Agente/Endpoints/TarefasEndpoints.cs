using System.Security.Cryptography;
using System.Text;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Operacao;
using Api.Agente.Agendamento;
using Api.Agente.Observabilidade;

namespace Api.Agente.Endpoints;

public sealed record ResultadoTarefaColeta(string CorrelacaoId, int Fontes, int NovasNovidades, int Falhas);

public sealed record ResultadoTarefaRetencao(string CorrelacaoId, IReadOnlyDictionary<string, int> RemovidosPorTabela);

/// <summary>
/// Gatilhos de coleta e retenção para hospedagem sem processo contínuo (ADR 0008). Só chamam os casos
/// de uso, com o mesmo bloqueio dos agendadores. Sem Agendamento:TokenGatilho, as rotas não existem.
/// </summary>
public static class TarefasEndpoints
{
    public static IEndpointRouteBuilder MapTarefas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/tarefas")
            .AddEndpointFilter(ExigirToken)
            .ExcludeFromDescription();

        // Usa o ciclo de vida da aplicação: a queda do cliente HTTP não deve interromper uma coleta em andamento.
        grupo.MapPost("/coleta", async (HttpContext http, IBloqueioExecucao bloqueio, ExecutarCicloColeta ciclo, IHostApplicationLifetime vida) =>
        {
            await using var trava = await bloqueio.TentarAdquirirAsync(AgendadorColeta.NomeBloqueio, vida.ApplicationStopping);
            if (trava is null)
            {
                return Results.Conflict();
            }

            var correlacao = http.CorrelacaoId();
            var resultados = await ciclo.ExecutarAsync(correlacao, vida.ApplicationStopping);
            return Results.Ok(new ResultadoTarefaColeta(
                correlacao,
                resultados.Count,
                resultados.Sum(r => r.NovasNovidades),
                resultados.Count(r => r.Status == StatusExecucao.FalhaTecnica)));
        });

        grupo.MapPost("/retencao", async (HttpContext http, IBloqueioExecucao bloqueio, AplicarRetencao retencao, IHostApplicationLifetime vida) =>
        {
            await using var trava = await bloqueio.TentarAdquirirAsync(AgendadorRetencao.NomeBloqueio, vida.ApplicationStopping);
            if (trava is null)
            {
                return Results.Conflict();
            }

            var resultado = await retencao.ExecutarAsync(vida.ApplicationStopping);
            return Results.Ok(new ResultadoTarefaRetencao(http.CorrelacaoId(), resultado.RemovidosPorTabela));
        });

        return app;
    }

    private static async ValueTask<object?> ExigirToken(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var http = contexto.HttpContext;
        var token = http.RequestServices.GetRequiredService<IConfiguration>()["Agendamento:TokenGatilho"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.NotFound();
        }

        return Iguais(http.Request.Headers.Authorization.ToString(), $"Bearer {token}")
            ? await proximo(contexto)
            : Results.Unauthorized();
    }

    /// <summary>Compara os hashes em tempo constante, sem revelar o conteúdo nem o tamanho do token.</summary>
    private static bool Iguais(string recebido, string esperado) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(recebido)),
            SHA256.HashData(Encoding.UTF8.GetBytes(esperado)));
}
