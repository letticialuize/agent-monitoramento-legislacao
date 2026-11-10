using Api.Agente.Observabilidade;

namespace Api.Agente.Seguranca;

/// <summary>
/// CORS para o frontend da SOFIA no navegador (feature 003). Só vale para chamadas feitas por browser:
/// a SOFIA backend chama o agente por HTTP direto, sem o cabeçalho Origin, e não depende desta política.
/// CORS não autentica ninguém; o controle de acesso segue o ADR 0007.
/// </summary>
public static class CorsSofia
{
    public const string Politica = "SofiaBrowser";
    public const string Secao = "Cors:AllowedOrigins";

    /// <summary>
    /// Registra a política com as origens de <c>Cors:AllowedOrigins</c>. Lista vazia: nenhuma origem
    /// recebe permissão. Origem inválida (curinga, path, barra final) impede a subida.
    /// </summary>
    public static IServiceCollection AddCorsSofia(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = LerOrigens(configuracao);

        services.AddCors(o => o.AddPolicy(Politica, p => p
            .WithOrigins(origens)
            .WithMethods("POST")
            .WithHeaders("Content-Type", MiddlewareCorrelacao.Cabecalho)
            .WithExposedHeaders(MiddlewareCorrelacao.Cabecalho)
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    /// <summary>Aplica a política só às rotas usadas pelo frontend da SOFIA.</summary>
    public static TBuilder RequireCorsSofia<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequireCors(Politica);

    public static string[] LerOrigens(IConfiguration configuracao)
    {
        var valores = configuracao.GetSection(Secao).Get<string[]>() ?? [];
        var origens = valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var origem in origens)
        {
            if (!EhOrigemValida(origem))
            {
                throw new InvalidOperationException(
                    $"{Secao} contém um valor inválido: '{origem}'. Use só esquema, host e porta (ex.: https://sofia.exemplo.com), sem curinga, caminho ou barra final.");
            }
        }

        return origens;
    }

    private static bool EhOrigemValida(string origem) =>
        !origem.Contains('*')
        && !origem.EndsWith('/')
        && Uri.TryCreate(origem, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
}
