using Agente.Domain.Catalogo;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.EntityFrameworkCore;

namespace Agente.Infrastructure.Persistencia;

/// <summary>Banco PostgreSQL da Api.Agente (proprietário exclusivo dos seus dados, Princípio XIII).</summary>
public sealed class AgenteDbContext(DbContextOptions<AgenteDbContext> options) : DbContext(options)
{
    public DbSet<VersaoCatalogo> VersoesCatalogo => Set<VersaoCatalogo>();

    public DbSet<Fonte> Fontes => Set<Fonte>();

    public DbSet<EnteMonitorado> EntesMonitorados => Set<EnteMonitorado>();

    public DbSet<Novidade> Novidades => Set<Novidade>();

    public DbSet<OcorrenciaNovidade> Ocorrencias => Set<OcorrenciaNovidade>();

    public DbSet<RevisaoNovidade> Revisoes => Set<RevisaoNovidade>();

    public DbSet<ExecucaoColeta> ExecucoesColeta => Set<ExecucaoColeta>();

    public DbSet<Consulta> Consultas => Set<Consulta>();

    public DbSet<EtapaExecucao> Etapas => Set<EtapaExecucao>();

    public DbSet<RegistroConsumo> RegistrosConsumo => Set<RegistroConsumo>();

    public DbSet<TabelaPreco> TabelasPreco => Set<TabelaPreco>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenteDbContext).Assembly);
    }
}
