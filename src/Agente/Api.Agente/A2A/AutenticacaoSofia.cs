using System.Security.Cryptography;
using System.Text;

namespace Api.Agente.A2A;

/// <summary>
/// Chave Bearer compartilhada com a SOFIA (ADR 0010). Protege o Agent Card e o endpoint A2A.
/// O valor vem só da configuração <c>SOFIA_A2A_TOKEN</c>; vazio, a integração fica indisponível (503),
/// nunca liberada. É uma chave opaca: não é JWT e não identifica o aluno nem o usuário.
/// </summary>
public static class AutenticacaoSofia
{
    public const string ChaveConfiguracao = "SOFIA_A2A_TOKEN";

    public static TBuilder RequireTokenSofia<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.AddEndpointFilter(Filtrar);

    private static async ValueTask<object?> Filtrar(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var http = contexto.HttpContext;
        var esperado = http.RequestServices.GetRequiredService<IConfiguration>()[ChaveConfiguracao];
        if (string.IsNullOrEmpty(esperado))
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Api.Agente.A2A")
                .LogError("A2A indisponível: {Chave} não configurada.", ChaveConfiguracao);
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Integração A2A indisponível.");
        }

        if (!TokenValido(http.Request.Headers.Authorization.ToString(), esperado))
        {
            // Antes de ler o corpo ou chamar o LLM. Sem detalhar se faltou o cabeçalho ou a chave está errada.
            http.Response.Headers.WWWAuthenticate = "Bearer";
            return Results.Unauthorized();
        }

        return await proximo(contexto);
    }

    /// <summary>Esquema "Bearer" sem distinção de maiúsculas (RFC 7235); a chave, exata e em tempo constante.</summary>
    internal static bool TokenValido(string? cabecalho, string esperado)
    {
        const string prefixo = "Bearer ";
        if (string.IsNullOrEmpty(cabecalho) || !cabecalho.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var recebido = cabecalho[prefixo.Length..];
        return recebido.Length > 0 && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(recebido)),
            SHA256.HashData(Encoding.UTF8.GetBytes(esperado)));
    }
}
