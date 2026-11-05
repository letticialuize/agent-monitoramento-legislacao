using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agente.Infrastructure.Persistencia;

/// <summary>Usada só pelo "dotnet ef" para gerar migrações (não abre conexão).</summary>
internal sealed class FabricaDesignTime : IDesignTimeDbContextFactory<AgenteDbContext>
{
    public AgenteDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AgenteDbContext>()
            .UseNpgsql("Host=localhost;Database=agente_design;Username=design;Password=design")
            .Options);
}
