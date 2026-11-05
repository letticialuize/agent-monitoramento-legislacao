namespace Agente.Domain.Monitoramento;

public interface IRepositorioNovidades
{
    Task<Novidade?> ObterAsync(Guid id, CancellationToken ct);

    Task<Novidade?> ObterPorChaveAsync(ChaveDeduplicacao chave, CancellationToken ct);

    Task<IReadOnlyList<Novidade>> ListarAguardandoClassificacaoIaAsync(int limite, CancellationToken ct);

    void Adicionar(Novidade novidade);
}
