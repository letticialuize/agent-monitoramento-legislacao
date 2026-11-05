using Agente.Domain.Catalogo;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.EntityFrameworkCore;

namespace Agente.Infrastructure.Persistencia.Repositorios;

internal sealed class RepositorioVersoesCatalogo(AgenteDbContext db) : IRepositorioVersoesCatalogo
{
    public Task<VersaoCatalogo?> ObterVigenteAsync(CancellationToken ct) =>
        db.VersoesCatalogo.FirstOrDefaultAsync(v => v.Vigente, ct);

    public Task<VersaoCatalogo?> ObterPorVersaoAsync(string versao, CancellationToken ct) =>
        db.VersoesCatalogo.FirstOrDefaultAsync(v => v.Versao == versao, ct);

    public Task<bool> ExisteAlgumaAsync(CancellationToken ct) => db.VersoesCatalogo.AnyAsync(ct);

    public void Adicionar(VersaoCatalogo versao) => db.VersoesCatalogo.Add(versao);
}

internal sealed class RepositorioEntesMonitorados(AgenteDbContext db) : IRepositorioEntesMonitorados
{
    public async Task<IReadOnlyList<EnteMonitorado>> ListarAsync(CancellationToken ct) =>
        await db.EntesMonitorados.OrderBy(e => e.CriadoEm).ToListAsync(ct);

    public Task<EnteMonitorado?> ObterAsync(Guid id, CancellationToken ct) =>
        db.EntesMonitorados.FirstOrDefaultAsync(e => e.Id == id, ct);

    public void Adicionar(EnteMonitorado ente) => db.EntesMonitorados.Add(ente);
}

internal sealed class RepositorioNovidades(AgenteDbContext db) : IRepositorioNovidades
{
    public Task<Novidade?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Novidades.FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<Novidade?> ObterPorChaveAsync(ChaveDeduplicacao chave, CancellationToken ct) =>
        db.Novidades.FirstOrDefaultAsync(n => n.ChaveDeduplicacao == chave, ct);

    public async Task<IReadOnlyList<Novidade>> ListarAguardandoClassificacaoIaAsync(int limite, CancellationToken ct) =>
        await db.Novidades
            .Where(n => n.AguardandoClassificacaoIa && n.Situacao != SituacaoNovidade.Descartada)
            .OrderBy(n => n.CriadoEm)
            .Take(limite)
            .ToListAsync(ct);

    public void Adicionar(Novidade novidade) => db.Novidades.Add(novidade);
}

internal sealed class RepositorioExecucoesColeta(AgenteDbContext db) : IRepositorioExecucoesColeta
{
    public async Task<DateTimeOffset?> UltimaExecucaoBemSucedidaAsync(Guid fonteId, CancellationToken ct) =>
        await db.ExecucoesColeta
            .Where(e => e.FonteId == fonteId && (e.Status == StatusExecucao.Sucesso || e.Status == StatusExecucao.Parcial))
            .MaxAsync(e => (DateTimeOffset?)e.IniciadaEm, ct);

    public async Task<IReadOnlyList<ExecucaoColeta>> FalhasDesdeAsync(DateTimeOffset desde, CancellationToken ct) =>
        await db.ExecucoesColeta
            .Where(e => e.Status == StatusExecucao.FalhaTecnica && e.IniciadaEm >= desde)
            .ToListAsync(ct);

    public void Adicionar(ExecucaoColeta execucao) => db.ExecucoesColeta.Add(execucao);
}

internal sealed class RepositorioConsultas(AgenteDbContext db) : IRepositorioConsultas
{
    public void Adicionar(Consulta consulta) => db.Consultas.Add(consulta);
}

internal sealed class RepositorioConsumo(AgenteDbContext db) : IRepositorioConsumo
{
    public void Adicionar(RegistroConsumo registro) => db.RegistrosConsumo.Add(registro);
}

internal sealed class RepositorioTabelasPreco(AgenteDbContext db) : IRepositorioTabelasPreco
{
    public Task<TabelaPreco?> ObterVigenteAsync(string modelo, DateTimeOffset em, CancellationToken ct) =>
        db.TabelasPreco
            .Where(t => t.Modelo == modelo && t.VigenteDesde <= em)
            .OrderByDescending(t => t.VigenteDesde)
            .FirstOrDefaultAsync(ct);
}
