using Agente.Domain.Comum;

namespace Agente.Domain.Operacao;

public enum StatusExecucao { EmAndamento, Sucesso, Parcial, FalhaTecnica }

/// <summary>Status do processamento devolvido no envelope.</summary>
public enum StatusProcessamento { Concluido, ConcluidoComLimitacoes, FalhaTecnica, RejeitadoValidacao }

public enum EtapaFluxo { Coleta, Classificacao, CruzamentoCliente, Validacoes, Ferramentas, Llm, ValidacaoSaida, Resposta }

public enum StatusEtapa { EmAndamento, Sucesso, Falha, NaoExecutada }

public enum FinalidadeLlm { Classificacao, RelacaoNicho, ResumoBoletim, Conversa }

/// <summary>Período de consulta (início ≤ fim).</summary>
public sealed record PeriodoConsulta : ObjetoDeValor
{
    public DateOnly Inicio { get; }

    public DateOnly Fim { get; }

    private PeriodoConsulta(DateOnly inicio, DateOnly fim)
    {
        Inicio = inicio;
        Fim = fim;
    }

    public static Resultado<PeriodoConsulta> Criar(DateOnly inicio, DateOnly fim) =>
        inicio > fim
            ? Resultado<PeriodoConsulta>.Falha("periodo.invalido", "O início do período deve ser anterior ou igual ao fim.")
            : Resultado<PeriodoConsulta>.Ok(new PeriodoConsulta(inicio, fim));

    /// <summary>Últimos <paramref name="dias"/> dias corridos até <paramref name="hoje"/>, inclusive.</summary>
    public static PeriodoConsulta UltimosDias(DateOnly hoje, int dias) => new(hoje.AddDays(-(Math.Max(dias, 1) - 1)), hoje);
}

/// <summary>Agregado: uma execução de coleta de uma fonte.</summary>
public sealed class ExecucaoColeta : RaizAgregado
{
    public string CorrelacaoId { get; private set; } = string.Empty;

    public Guid FonteId { get; private set; }

    public DateTimeOffset IniciadaEm { get; private set; }

    public DateTimeOffset? FinalizadaEm { get; private set; }

    public StatusExecucao Status { get; private set; }

    public string? Erro { get; private set; }

    public int NovidadesEncontradas { get; private set; }

    private ExecucaoColeta() { }

    public static ExecucaoColeta Iniciar(Guid fonteId, string correlacaoId, DateTimeOffset agora) => new()
    {
        Id = Guid.NewGuid(),
        FonteId = fonteId,
        CorrelacaoId = correlacaoId,
        IniciadaEm = agora,
        Status = StatusExecucao.EmAndamento,
        CriadoEm = agora,
        AtualizadoEm = agora,
    };

    public void Concluir(int novidadesEncontradas, bool parcial, string? erro, DateTimeOffset agora)
    {
        NovidadesEncontradas = novidadesEncontradas;
        Status = parcial ? StatusExecucao.Parcial : StatusExecucao.Sucesso;
        Erro = erro;
        FinalizadaEm = agora;
        Tocar(agora);
    }

    public void Falhar(string erro, DateTimeOffset agora)
    {
        Status = StatusExecucao.FalhaTecnica;
        Erro = erro;
        FinalizadaEm = agora;
        Tocar(agora);
    }
}

/// <summary>Agregado: pedido sob demanda (consulta estruturada ou mensagem de conversa).</summary>
public sealed class Consulta : RaizAgregado
{
    private readonly List<EtapaExecucao> _etapas = [];

    public string CorrelacaoId { get; private set; } = string.Empty;

    public string UsuarioId { get; private set; } = string.Empty;

    public string? ConversaId { get; private set; }

    public string Assunto { get; private set; } = string.Empty;

    public DateOnly PeriodoInicio { get; private set; }

    public DateOnly PeriodoFim { get; private set; }

    public IReadOnlyList<Esfera> Esferas { get; private set; } = [];

    public IReadOnlyList<Perfil> Perfis { get; private set; } = [];

    public IReadOnlyList<Grupo> Grupos { get; private set; } = [];

    public Guid? ClienteId { get; private set; }

    public StatusProcessamento? Status { get; private set; }

    public bool RequerRevisaoHumana { get; private set; }

    public IReadOnlyList<string> Limitacoes { get; private set; } = [];

    public string VersaoContrato { get; private set; } = string.Empty;

    public DateTimeOffset? FinalizadaEm { get; private set; }

    public IReadOnlyList<EtapaExecucao> Etapas => _etapas;

    private Consulta() { }

    public static Consulta Iniciar(
        string correlacaoId,
        string usuarioId,
        string? conversaId,
        string assunto,
        PeriodoConsulta periodo,
        IReadOnlyList<Esfera> esferas,
        IReadOnlyList<Perfil> perfis,
        IReadOnlyList<Grupo> grupos,
        string versaoContrato,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        return new Consulta
        {
            Id = Guid.NewGuid(),
            CorrelacaoId = correlacaoId,
            UsuarioId = usuarioId,
            ConversaId = conversaId,
            Assunto = assunto,
            PeriodoInicio = periodo.Inicio,
            PeriodoFim = periodo.Fim,
            Esferas = esferas.ToList(),
            Perfis = perfis.ToList(),
            Grupos = grupos.ToList(),
            VersaoContrato = versaoContrato,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public EtapaExecucao IniciarEtapa(EtapaFluxo etapa, DateTimeOffset agora)
    {
        var e = new EtapaExecucao(Guid.NewGuid(), Id, etapa, agora);
        _etapas.Add(e);
        Tocar(agora);
        return e;
    }

    public void Finalizar(StatusProcessamento status, bool requerRevisaoHumana, IEnumerable<string> limitacoes, DateTimeOffset agora)
    {
        Status = status;
        RequerRevisaoHumana = requerRevisaoHumana;
        Limitacoes = limitacoes.ToList();
        FinalizadaEm = agora;
        Tocar(agora);
    }
}

/// <summary>Etapa do fluxo obrigatório (entidade interna de <see cref="Consulta"/>).</summary>
public sealed class EtapaExecucao : Entidade
{
    public Guid ConsultaId { get; private set; }

    public EtapaFluxo Etapa { get; private set; }

    public StatusEtapa Status { get; private set; }

    public DateTimeOffset IniciadaEm { get; private set; }

    public DateTimeOffset? FinalizadaEm { get; private set; }

    public string? Detalhe { get; private set; }

    private EtapaExecucao() { }

    internal EtapaExecucao(Guid id, Guid consultaId, EtapaFluxo etapa, DateTimeOffset agora) : base(id)
    {
        ConsultaId = consultaId;
        Etapa = etapa;
        Status = StatusEtapa.EmAndamento;
        IniciadaEm = agora;
    }

    public void Finalizar(StatusEtapa status, string? detalhe, DateTimeOffset agora)
    {
        Status = status;
        Detalhe = detalhe is { Length: > 2000 } ? detalhe[..2000] : detalhe;
        FinalizadaEm = agora;
    }
}

/// <summary>
/// Agregado: consumo de uma chamada ao LLM (Princípio XV). Uso desconhecido é registrado como
/// nulo com <see cref="UsoConhecido"/> = false, nunca como zero.
/// </summary>
public sealed class RegistroConsumo : RaizAgregado
{
    public Guid ExecucaoId { get; private set; }

    public string CorrelacaoId { get; private set; } = string.Empty;

    public string Provedor { get; private set; } = string.Empty;

    public string Modelo { get; private set; } = string.Empty;

    public FinalidadeLlm Finalidade { get; private set; }

    public int Tentativa { get; private set; }

    public int? TokensEntrada { get; private set; }

    public int? TokensSaida { get; private set; }

    public bool UsoConhecido { get; private set; }

    public DateTimeOffset Inicio { get; private set; }

    public long DuracaoMs { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public decimal? CustoEstimado { get; private set; }

    public decimal? CustoInformado { get; private set; }

    public string? Moeda { get; private set; }

    public Guid? TabelaPrecoId { get; private set; }

    private RegistroConsumo() { }

    public static RegistroConsumo Registrar(
        Guid execucaoId,
        string correlacaoId,
        string provedor,
        string modelo,
        FinalidadeLlm finalidade,
        int tentativa,
        int? tokensEntrada,
        int? tokensSaida,
        DateTimeOffset inicio,
        TimeSpan duracao,
        string status,
        TabelaPreco? tabela,
        DateTimeOffset agora)
    {
        var conhecido = tokensEntrada is not null && tokensSaida is not null;
        decimal? custo = null;
        if (conhecido && tabela is not null)
        {
            custo = tabela.Calcular(tokensEntrada!.Value, tokensSaida!.Value);
        }

        return new RegistroConsumo
        {
            Id = Guid.NewGuid(),
            ExecucaoId = execucaoId,
            CorrelacaoId = correlacaoId,
            Provedor = provedor,
            Modelo = modelo,
            Finalidade = finalidade,
            Tentativa = tentativa,
            TokensEntrada = conhecido ? tokensEntrada : null,
            TokensSaida = conhecido ? tokensSaida : null,
            UsoConhecido = conhecido,
            Inicio = inicio,
            DuracaoMs = (long)duracao.TotalMilliseconds,
            Status = status,
            CustoEstimado = custo,
            CustoInformado = null,
            Moeda = custo is null ? null : tabela!.Moeda,
            TabelaPrecoId = custo is null ? null : tabela!.Id,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }
}

/// <summary>Agregado: preço vigente de um modelo, com referência da página consultada.</summary>
public sealed class TabelaPreco : RaizAgregado
{
    public string Modelo { get; private set; } = string.Empty;

    public decimal PrecoEntradaPorMilhao { get; private set; }

    public decimal PrecoSaidaPorMilhao { get; private set; }

    public string Moeda { get; private set; } = string.Empty;

    public DateTimeOffset VigenteDesde { get; private set; }

    public string UrlReferencia { get; private set; } = string.Empty;

    private TabelaPreco() { }

    public static Resultado<TabelaPreco> Criar(string modelo, decimal entrada, decimal saida, string moeda, DateTimeOffset vigenteDesde, string urlReferencia, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(moeda) || string.IsNullOrWhiteSpace(urlReferencia) || entrada < 0 || saida < 0)
        {
            return Resultado<TabelaPreco>.Falha("preco.invalido", "Modelo, moeda, URL de referência e preços não negativos são obrigatórios.");
        }

        return Resultado<TabelaPreco>.Ok(new TabelaPreco
        {
            Id = Guid.NewGuid(),
            Modelo = modelo,
            PrecoEntradaPorMilhao = entrada,
            PrecoSaidaPorMilhao = saida,
            Moeda = moeda,
            VigenteDesde = vigenteDesde,
            UrlReferencia = urlReferencia,
            CriadoEm = agora,
            AtualizadoEm = agora,
        });
    }

    public decimal Calcular(int tokensEntrada, int tokensSaida) =>
        Math.Round((tokensEntrada * PrecoEntradaPorMilhao + tokensSaida * PrecoSaidaPorMilhao) / 1_000_000m, 6);
}

public interface IRepositorioExecucoesColeta
{
    /// <summary>Início da última execução com sucesso (total ou parcial) da fonte.</summary>
    Task<DateTimeOffset?> UltimaExecucaoBemSucedidaAsync(Guid fonteId, CancellationToken ct);

    /// <summary>Execuções com falha técnica cujo início está no intervalo.</summary>
    Task<IReadOnlyList<ExecucaoColeta>> FalhasDesdeAsync(DateTimeOffset desde, CancellationToken ct);

    void Adicionar(ExecucaoColeta execucao);
}

public interface IRepositorioConsultas
{
    void Adicionar(Consulta consulta);
}

public interface IRepositorioConsumo
{
    void Adicionar(RegistroConsumo registro);
}

public interface IRepositorioTabelasPreco
{
    Task<TabelaPreco?> ObterVigenteAsync(string modelo, DateTimeOffset em, CancellationToken ct);
}
