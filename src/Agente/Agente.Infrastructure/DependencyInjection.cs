using Agente.Application.Comum;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Catalogo;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Agente.Infrastructure.Conectores;
using Agente.Infrastructure.Llm;
using Agente.Infrastructure.Memoria;
using Agente.Infrastructure.Persistencia;
using Agente.Infrastructure.Persistencia.Consultas;
using Agente.Infrastructure.Persistencia.Repositorios;
using Agente.Infrastructure.Regras;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MongoDB.Driver;
using Npgsql;
using Polly;

namespace Agente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Adaptadores das portas: PostgreSQL, MongoDB, conectores, LLM e regras versionadas.</summary>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        services.Configure<OpcoesLlm>(configuracao.GetSection("Llm"));
        services.Configure<OpcoesRegras>(configuracao.GetSection("Regras"));

        // PostgreSQL
        var postgres = configuracao.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(postgres))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres não configurada (use user-secrets ou variável de ambiente).");
        }

        var dataSource = new NpgsqlDataSourceBuilder(postgres).Build();
        services.AddSingleton(dataSource);
        services.AddDbContext<AgenteDbContext>(o => o.UseNpgsql(dataSource), optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<AgenteDbContext>(o => o.UseNpgsql(dataSource));

        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddSingleton<IBloqueioExecucao, BloqueioExecucaoPostgres>();
        services.AddScoped<IRepositorioVersoesCatalogo, RepositorioVersoesCatalogo>();
        services.AddScoped<IRepositorioEntesMonitorados, RepositorioEntesMonitorados>();
        services.AddScoped<IRepositorioNovidades, RepositorioNovidades>();
        services.AddScoped<IRepositorioExecucoesColeta, RepositorioExecucoesColeta>();
        services.AddScoped<IRepositorioConsultas, RepositorioConsultas>();
        services.AddScoped<IRepositorioConsumo, RepositorioConsumo>();
        services.AddScoped<IRepositorioTabelasPreco, RepositorioTabelasPreco>();
        services.AddScoped<IConsultaNovidades, ConsultaNovidades>();
        services.AddScoped<IConsultaSituacaoFontes, ConsultaSituacaoFontes>();
        services.AddScoped<IConsultaConsumo, ConsultaConsumo>();
        services.AddScoped<IExpurgoOperacional, ExpurgoOperacional>();

        // Tempo
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRelogio>(sp => new RelogioSistema(
            sp.GetRequiredService<TimeProvider>(),
            TimeZoneInfo.FindSystemTimeZoneById(configuracao["Boletim:Fuso"] ?? "America/Sao_Paulo")));

        // Regras versionadas
        services.AddSingleton<IProvedorRegras, ProvedorRegrasVersionadas>();

        // MongoDB (memória conversacional)
        var mongo = configuracao.GetConnectionString("Mongo");
        if (string.IsNullOrWhiteSpace(mongo))
        {
            throw new InvalidOperationException("ConnectionStrings:Mongo não configurada.");
        }

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo));
        services.AddSingleton<IRepositorioConversas, RepositorioConversas>();

        // LLM
        services.AddSingleton<FabricaChatOpenAi>();
        services.AddScoped<IRegistradorConsumo, RegistradorConsumo>();
        services.AddScoped<IClienteLlmEstruturado, ClienteLlmSemanticKernel>();
        services.AddScoped<IOrquestradorConversa, OrquestradorConversaSemanticKernel>();

        // Conectores (INLABS e LexML bloqueados até confirmação do formato: research R-03)
        services.AddMemoryCache();
        services.AddTransient<HandlerListaPermissao>();
        var timeout = TimeSpan.FromSeconds(Math.Max(5, configuracao.GetValue("Coleta:TimeoutPadraoSegundos", 60)));
        var tentativas = Math.Max(0, configuracao.GetValue("Coleta:TentativasPorFonte", 2));
        services.AddHttpClient(Ajudas.ClienteHttp, c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("infis-agente-monitoramento-legislacao/1.0");
                c.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<HandlerListaPermissao>()
            .AddResilienceHandler("coleta", b =>
            {
                b.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = Math.Max(1, tentativas), UseJitter = true, ShouldRetryAfterHeader = true });
                b.AddTimeout(timeout);
            });

        services.AddScoped<IConectorFonte, SenadoConector>();
        services.AddScoped<IConectorFonte, CamaraConector>();
        services.AddScoped<IConectorFonte, DjenConector>();
        services.AddScoped<IConectorFonte, QueridoDiarioConector>();
        services.AddScoped<IConectorFonte, PaginaOficialConector>();

        return services;
    }

    /// <summary>Aplica migrações e o esquema da busca textual (função imutável + índice GIN).</summary>
    public static async Task PrepararBancoAsync(IServiceProvider servicos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        using var escopo = servicos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgenteDbContext>();
        await db.Database.MigrateAsync(ct);
        await EsquemaBusca.GarantirAsync(db, ct);
    }
}
