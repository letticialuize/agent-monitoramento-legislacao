using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Agente.Observabilidade;

/// <summary>Correlação de requisições (Princípio V): propaga ou gera X-Correlation-Id.</summary>
public sealed partial class MiddlewareCorrelacao(RequestDelegate proximo, ILogger<MiddlewareCorrelacao> logger)
{
    public const string Cabecalho = "X-Correlation-Id";
    public const string ChaveItem = "correlacaoId";

    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var recebido = contexto.Request.Headers[Cabecalho].ToString();
        var correlacao = !string.IsNullOrWhiteSpace(recebido) && Valido().IsMatch(recebido)
            ? recebido
            : Guid.NewGuid().ToString("N");

        contexto.Items[ChaveItem] = correlacao;
        contexto.Response.Headers[Cabecalho] = correlacao;
        Activity.Current?.SetTag("correlacao.id", correlacao);

        using (logger.BeginScope(new Dictionary<string, object> { ["correlacaoId"] = correlacao }))
        {
            await proximo(contexto);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex Valido();
}

public static class ExtensoesObservabilidade
{
    public static string CorrelacaoId(this HttpContext contexto) =>
        contexto.Items.TryGetValue(MiddlewareCorrelacao.ChaveItem, out var v) && v is string s ? s : Guid.NewGuid().ToString("N");

    /// <summary>Logs JSON estruturados com escopos (correlação) e OpenTelemetry (traces e métricas).</summary>
    public static WebApplicationBuilder AddObservabilidadeAgente(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });

        var otlp = builder.Configuration["OpenTelemetry:Endpoint"];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("infis-agente-monitoramento-legislacao"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
            });

        return builder;
    }
}
