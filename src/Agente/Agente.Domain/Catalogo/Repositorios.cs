namespace Agente.Domain.Catalogo;

public interface IRepositorioVersoesCatalogo
{
    Task<VersaoCatalogo?> ObterVigenteAsync(CancellationToken ct);

    Task<VersaoCatalogo?> ObterPorVersaoAsync(string versao, CancellationToken ct);

    Task<bool> ExisteAlgumaAsync(CancellationToken ct);

    void Adicionar(VersaoCatalogo versao);
}

public interface IRepositorioEntesMonitorados
{
    Task<IReadOnlyList<EnteMonitorado>> ListarAsync(CancellationToken ct);

    Task<EnteMonitorado?> ObterAsync(Guid id, CancellationToken ct);

    void Adicionar(EnteMonitorado ente);
}
