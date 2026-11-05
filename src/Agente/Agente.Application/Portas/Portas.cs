using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;

namespace Agente.Application.Portas;

/// <summary>Confirma as alterações dos agregados carregados no caso de uso.</summary>
public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct);
}

/// <summary>Tempo injetável: UTC e conversão para o fuso de negócio (Boletim:Fuso).</summary>
public interface IRelogio
{
    DateTimeOffset AgoraUtc { get; }

    DateTimeOffset ParaLocal(DateTimeOffset instante);

    DateOnly HojeLocal { get; }
}

/// <summary>Garante execução única de tarefas agendadas entre instâncias.</summary>
public interface IBloqueioExecucao
{
    /// <summary>Devolve o bloqueio adquirido (liberado no Dispose) ou null se outra instância o detém.</summary>
    Task<IAsyncDisposable?> TentarAdquirirAsync(string nome, CancellationToken ct);
}

// ---------- Conectores de fontes ----------

/// <summary>Fonte do catálogo vista pelo conector.</summary>
public sealed record FonteParaColeta(
    Guid Id,
    string Codigo,
    string Nome,
    string Url,
    string OrgaoEmissor,
    NaturezaFonte Natureza,
    TipoConector TipoConector,
    string? Configuracao);

/// <summary>Ato encontrado por um conector.</summary>
public sealed record ItemColetado(
    DateOnly DataPublicacao,
    string OrgaoEmissor,
    string TipoAto,
    string? NumeroAto,
    string Ementa,
    string Url,
    string? Trecho = null);

public sealed record ResultadoConector(IReadOnlyList<ItemColetado> Itens, bool Parcial, string? Aviso);

public interface IConectorFonte
{
    TipoConector Tipo { get; }

    Task<ResultadoConector> ColetarAsync(FonteParaColeta fonte, DateOnly desde, DateOnly ate, CancellationToken ct);
}

// ---------- LLM ----------

/// <summary>Contexto de rastreabilidade de uma chamada ao LLM.</summary>
public sealed record ContextoExecucao(Guid ExecucaoId, string CorrelacaoId);

public sealed record RequisicaoLlm(
    ContextoExecucao Contexto,
    FinalidadeLlm Finalidade,
    string PromptSistema,
    string MensagemUsuario,
    string NomeSchema,
    string SchemaEstrito,
    int TentativaValidacao);

public sealed record RespostaLlm(bool Sucesso, string? Json, string? Erro, string Modelo);

/// <summary>Gera JSON conforme um schema estrito (Structured Outputs). Registra o consumo de cada tentativa.</summary>
public interface IClienteLlmEstruturado
{
    bool Configurado { get; }

    Task<RespostaLlm> GerarJsonAsync(RequisicaoLlm requisicao, CancellationToken ct);
}

public sealed record MensagemConversa(string Papel, string Texto, DateTimeOffset Em);

public sealed record ParametroFerramenta(string Nome, string Descricao, string Tipo, bool Obrigatorio);

/// <summary>Ferramenta exposta ao LLM; a execução é sempre feita pelo sistema (Princípio XII).</summary>
public sealed record DefinicaoFerramenta(
    string Nome,
    string Descricao,
    IReadOnlyList<ParametroFerramenta> Parametros,
    Func<IReadOnlyDictionary<string, string?>, CancellationToken, Task<string>> Executar);

public sealed record RequisicaoConversa(
    ContextoExecucao Contexto,
    string PromptSistema,
    IReadOnlyList<MensagemConversa> Historico,
    string MensagemUsuario,
    IReadOnlyList<DefinicaoFerramenta> Ferramentas);

public sealed record RespostaConversa(bool Sucesso, string? Texto, string? Erro);

/// <summary>Conversa com chamada de ferramentas (Function Calling). Registra o consumo de cada chamada.</summary>
public interface IOrquestradorConversa
{
    bool Configurado { get; }

    Task<RespostaConversa> ResponderAsync(RequisicaoConversa requisicao, CancellationToken ct);
}

public sealed record DadosConsumo(
    ContextoExecucao Contexto,
    string Provedor,
    string Modelo,
    FinalidadeLlm Finalidade,
    int Tentativa,
    int? TokensEntrada,
    int? TokensSaida,
    DateTimeOffset Inicio,
    TimeSpan Duracao,
    string Status);

/// <summary>Persiste o consumo imediatamente, mesmo que o caso de uso falhe depois.</summary>
public interface IRegistradorConsumo
{
    Task RegistrarAsync(DadosConsumo dados, CancellationToken ct);
}

// ---------- Memória conversacional ----------

public interface IRepositorioConversas
{
    Task<IReadOnlyList<MensagemConversa>> ObterUltimasAsync(string usuarioId, string conversaId, int maximo, CancellationToken ct);

    Task AcrescentarAsync(string usuarioId, string conversaId, IReadOnlyList<MensagemConversa> mensagens, CancellationToken ct);
}

// ---------- E-mail (Entrega 3) ----------

public sealed record MensagemEmail(string Para, string NomePara, string Assunto, string Html, string TextoPuro);

public interface IEnvioEmail
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct);
}

// ---------- Regras versionadas ----------

/// <summary>Sinônimos para expansão da busca textual (configuração versionada).</summary>
public sealed record DicionarioSinonimos(string Versao, IReadOnlyList<IReadOnlyList<string>> Grupos)
{
    /// <summary>Termos equivalentes ao assunto (inclui o próprio assunto).</summary>
    public IReadOnlyList<string> Expandir(string assunto)
    {
        var normalizado = Texto.Normalizar(assunto);
        var termos = new List<string> { assunto };
        foreach (var grupo in Grupos)
        {
            if (grupo.Any(t => Texto.Normalizar(t) == normalizado || Texto.ContemTermo(normalizado, t)))
            {
                termos.AddRange(grupo);
            }
        }

        return termos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public interface IProvedorRegras
{
    RegrasPerfil Perfis { get; }

    RegrasEsferaPorOrgao EsferaPorOrgao { get; }

    DicionarioSinonimos Sinonimos { get; }
}

// ---------- Retenção ----------

public sealed record ResultadoExpurgo(IReadOnlyDictionary<string, int> RemovidosPorTabela);

public interface IExpurgoOperacional
{
    Task<ResultadoExpurgo> ExpurgarAsync(DateTimeOffset limite, CancellationToken ct);
}
