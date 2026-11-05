using Agente.Application.Portas;
using Agente.Domain.Operacao;
using Agente.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agente.Infrastructure.Llm;

/// <summary>
/// Persiste cada chamada ao LLM num contexto próprio, para que o consumo fique registrado mesmo se o
/// caso de uso falhar depois (Princípio XV). Uso desconhecido fica nulo, nunca zero.
/// </summary>
internal sealed class RegistradorConsumo(
    IDbContextFactory<AgenteDbContext> fabrica,
    IRelogio relogio,
    ILogger<RegistradorConsumo> logger) : IRegistradorConsumo
{
    public async Task RegistrarAsync(DadosConsumo dados, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dados);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var tabela = await db.TabelasPreco
            .Where(t => t.Modelo == dados.Modelo && t.VigenteDesde <= dados.Inicio)
            .OrderByDescending(t => t.VigenteDesde)
            .FirstOrDefaultAsync(ct);

        if (tabela is null && dados.TokensEntrada is not null)
        {
            logger.LogWarning("Sem tabela de preço vigente para o modelo {Modelo}; custo não estimado.", dados.Modelo);
        }

        db.RegistrosConsumo.Add(RegistroConsumo.Registrar(
            dados.Contexto.ExecucaoId,
            dados.Contexto.CorrelacaoId,
            dados.Provedor,
            dados.Modelo,
            dados.Finalidade,
            dados.Tentativa,
            dados.TokensEntrada,
            dados.TokensSaida,
            dados.Inicio,
            dados.Duracao,
            dados.Status,
            tabela,
            relogio.AgoraUtc));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
