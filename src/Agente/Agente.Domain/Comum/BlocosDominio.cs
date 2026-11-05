namespace Agente.Domain.Comum;

/// <summary>Entidade com identidade estável.</summary>
public abstract class Entidade
{
    public Guid Id { get; protected set; }

    protected Entidade(Guid id) => Id = id;

    // Usado apenas pelo mapeamento de persistência.
    protected Entidade() { }
}

/// <summary>Raiz de agregado: único ponto de alteração do agregado e unidade de persistência.</summary>
public abstract class RaizAgregado : Entidade
{
    public DateTimeOffset CriadoEm { get; protected set; }

    public DateTimeOffset AtualizadoEm { get; protected set; }

    protected RaizAgregado(Guid id, DateTimeOffset agora) : base(id)
    {
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    protected RaizAgregado() { }

    protected void Tocar(DateTimeOffset agora) => AtualizadoEm = agora;
}

/// <summary>Objeto de valor: imutável e comparado por valor.</summary>
public abstract record ObjetoDeValor;

/// <summary>Erro de negócio previsível (não é exceção).</summary>
public sealed record ErroDominio(string Codigo, string Mensagem)
{
    public override string ToString() => $"{Codigo}: {Mensagem}";
}

/// <summary>Resultado de uma operação de negócio sem valor de retorno.</summary>
public class Resultado
{
    private readonly List<ErroDominio> _erros;

    protected Resultado(IEnumerable<ErroDominio> erros) => _erros = erros.ToList();

    public bool Sucesso => _erros.Count == 0;

    public IReadOnlyList<ErroDominio> Erros => _erros;

    public static Resultado Ok() => new([]);

    public static Resultado Falha(string codigo, string mensagem) => new([new ErroDominio(codigo, mensagem)]);

    public static Resultado Falha(IEnumerable<ErroDominio> erros) => new(erros);

    public static Resultado<T> Ok<T>(T valor) => Resultado<T>.Ok(valor);
}

/// <summary>Resultado de uma operação de negócio com valor.</summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    private Resultado(T? valor, IEnumerable<ErroDominio> erros) : base(erros) => _valor = valor;

    public T Valor => Sucesso
        ? _valor!
        : throw new InvalidOperationException("Resultado com falha não possui valor.");

    public static Resultado<T> Ok(T valor) => new(valor, []);

    public static new Resultado<T> Falha(string codigo, string mensagem) => new(default, [new ErroDominio(codigo, mensagem)]);

    public static new Resultado<T> Falha(IEnumerable<ErroDominio> erros) => new(default, erros);
}
