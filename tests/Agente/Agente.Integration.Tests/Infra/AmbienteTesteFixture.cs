using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Agente.Application.Monitoramento;
using Agente.Application.Portas;
using Agente.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace Agente.Integration.Tests.Infra;

/// <summary>PostgreSQL e MongoDB reais (Testcontainers), compartilhados pela suíte.</summary>
public sealed class ContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8").Build();

    public string Postgres(string banco) => new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = banco }.ConnectionString;

    public string Mongo => _mongo.GetConnectionString();

    public async Task InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync());

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mongo.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoIntegracao : ICollectionFixture<ContainersFixture>
{
    public const string Nome = "integracao";
}

/// <summary>Logs capturados para verificar ausência de segredos (T075).</summary>
public sealed class LogsCapturados : ILoggerProvider
{
    public ConcurrentQueue<string> Linhas { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(LogsCapturados dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            dono.Linhas.Enqueue($"{categoria}: {formatter(state, exception)} {exception}");
    }
}

/// <summary>
/// Api.Agente completa sobre os containers, com fontes e OpenAI simuladas pelo WireMock, relógio
/// falso fixado em 2026-10-29 12:00 (horário de Brasília).
/// </summary>
public sealed class AgenteApp : WebApplicationFactory<Program>
{
    public const string ChaveLlm = "sk-teste-NAO-USAR-0123456789";

    /// <summary>Chave da SOFIA só para testes (a real fica fora do repositório, em SOFIA_A2A_TOKEN).</summary>
    public const string TokenSofia = "token-sofia-teste-NAO-USAR";

    private readonly ContainersFixture _containers;
    private readonly bool _llmConfigurado;
    private readonly Dictionary<string, string?> _extras;

    public AgenteApp(ContainersFixture containers, bool llmConfigurado = false, Dictionary<string, string?>? extras = null)
    {
        _containers = containers;
        _llmConfigurado = llmConfigurado;
        _extras = extras ?? [];
        Fontes = WireMockServer.Start();
        OpenAi = WireMockServer.Start();
        Banco = $"agente_{Guid.NewGuid():N}";
        ArquivoCatalogo = CatalogoDeTeste.Escrever(Fontes.Url!);
    }

    public WireMockServer Fontes { get; }

    public WireMockServer OpenAi { get; }

    public string Banco { get; }

    public string ArquivoCatalogo { get; }

    public FakeTimeProvider Tempo { get; } = new(new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero));

    public LogsCapturados Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:Postgres", _containers.Postgres(Banco));
        builder.UseSetting("ConnectionStrings:Mongo", _containers.Mongo);
        builder.UseSetting("Memoria:BancoMongo", Banco);
        builder.UseSetting("Agendamento:Habilitado", "false");
        builder.UseSetting("Coleta:ArquivoCatalogoInicial", ArquivoCatalogo);
        builder.UseSetting("Coleta:AprovadorCatalogoInicial", "suite-de-testes");
        builder.UseSetting("Coleta:TentativasPorFonte", "1");
        builder.UseSetting("Coleta:TimeoutPadraoSegundos", "10");
        builder.UseSetting("Boletim:Fuso", "America/Sao_Paulo");
        builder.UseSetting("Llm:Modelo", _llmConfigurado ? "modelo-teste" : string.Empty);
        builder.UseSetting("Llm:ApiKey", _llmConfigurado ? ChaveLlm : string.Empty);
        builder.UseSetting("Llm:Endpoint", OpenAi.Url + "/v1");
        builder.UseSetting("Llm:TentativasTransitorias", "1");
        builder.UseSetting("SOFIA_A2A_TOKEN", TokenSofia);
        foreach (var (k, v) in _extras)
        {
            builder.UseSetting(k, v);
        }

        builder.ConfigureTestServices(s =>
        {
            // Só o relógio de negócio é falso; o TimeProvider global (usado pelo Polly nas esperas) continua real.
            s.AddSingleton<IRelogio>(new RelogioFalso(Tempo));
            s.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    /// <summary>Cliente como a SOFIA: com o Bearer da integração A2A (ou outro valor, para testar recusa).</summary>
    public HttpClient ClienteSofia(string? autorizacao = "Bearer " + TokenSofia)
    {
        var cliente = CreateClient();
        if (autorizacao is not null)
        {
            cliente.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", autorizacao);
        }

        return cliente;
    }

    /// <summary>Executa um ciclo de coleta (o agendador está desabilitado nos testes).</summary>
    public async Task ColetarAsync()
    {
        using var escopo = Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<ExecutarCicloColeta>().ExecutarAsync($"teste-{Guid.NewGuid():N}", CancellationToken.None);
    }

    public async Task<T> NoBanco<T>(Func<AgenteDbContext, Task<T>> acao)
    {
        using var escopo = Services.CreateScope();
        return await acao(escopo.ServiceProvider.GetRequiredService<AgenteDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Fontes.Stop();
            OpenAi.Stop();
            File.Delete(ArquivoCatalogo);
        }
    }
}

/// <summary>Relógio de negócio controlado pelo teste, no fuso America/Sao_Paulo.</summary>
public sealed class RelogioFalso(FakeTimeProvider tempo) : IRelogio
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTimeOffset AgoraUtc => tempo.GetUtcNow();

    public DateTimeOffset ParaLocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Fuso);

    public DateOnly HojeLocal => DateOnly.FromDateTime(ParaLocal(AgoraUtc).DateTime);
}

/// <summary>Catálogo de teste com fontes apontando para o WireMock (exceção de domínio aprovada só para testes).</summary>
public static class CatalogoDeTeste
{
    public static string Escrever(string baseUrl)
    {
        var fontes = new object[]
        {
            Fonte("T-SENADO", $"{baseUrl}/senado/", "Senado Federal", "federal", "legislacao_federal", "oficial", "SenadoDadosAbertos",
                new { baseUrl = $"{baseUrl}/senado/dadosabertos", maxNormasPorDia = 10 }),
            Fonte("T-CAMARA", $"{baseUrl}/camara/", "Câmara dos Deputados", "federal", "legislacao_federal", "oficial", "CamaraDadosAbertos",
                new { baseUrl = $"{baseUrl}/camara/api/v2", siglasTipo = new[] { "PLP" }, itensPorPagina = 100, maxPaginas = 2 }),
            Fonte("T-FALHA", $"{baseUrl}/falha/", "Órgão Fora do Ar", "federal", "tributos", "oficial", "CamaraDadosAbertos",
                new { baseUrl = $"{baseUrl}/falha/api/v2", itensPorPagina = 100, maxPaginas = 1 }),
            Fonte("T-QD", $"{baseUrl}/qd/", "Open Knowledge Brasil (agregador)", "nacional", "legislacao_municipal", "agregador_apoio", "QueridoDiario",
                new { baseUrl = $"{baseUrl}/qd", territoryIds = new[] { "3550308" }, querystring = "ISS", size = 10 }),
            Fonte("T-PAGINA", $"{baseUrl}/pagina/", "Secretaria de Teste", "federal", "tributos", "oficial", "PaginaOficial",
                new { seletorItem = "li.ato", seletorTitulo = ".titulo", seletorData = ".data", padraoNumero = "n[ºo]\\s*(?<numero>[\\d.]+)", tipoAto = "Portaria" }),
            Fonte("T-INLABS", $"{baseUrl}/dou/", "Imprensa Nacional", "federal", "legislacao_federal", "oficial", "Inlabs", null),
        };

        var caminho = Path.Combine(Path.GetTempPath(), $"catalogo-teste-{Guid.NewGuid():N}.json");
        File.WriteAllText(caminho, JsonSerializer.Serialize(new { versao = "teste-1", documentoOrigem = "tests", fontes }));
        return caminho;
    }

    private static object Fonte(string codigo, string link, string orgao, string esfera, string grupo, string natureza, string conector, object? cfg) => new
    {
        codigo,
        nome = codigo,
        linkOficial = link,
        oQueMonitorar = "atos",
        orgaoEmissor = orgao,
        esfera,
        grupo,
        natureza,
        responsavelInterno = new[] { "AJ" },
        formatos = new[] { "json" },
        tipoConector = conector,
        configuracaoConector = cfg,
        periodicidade = "diaria",
        excecaoDominioAprovadaPor = natureza == "oficial" ? "suite-de-testes" : null,
    };

    public static string Data(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
