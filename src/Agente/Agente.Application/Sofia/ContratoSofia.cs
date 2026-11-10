namespace Agente.Application.Sofia;

// Contrato de negócio SOFIA v1.0 (feature 004). Independe do transporte: o A2A só carrega estes
// objetos serializados em TextPart. Os nomes JSON (camelCase) são os acordados pela turma; não renomear.

/// <summary>Pedido da SOFIA a um agente especializado.</summary>
public sealed record SolicitacaoAnaliseSofia(
    string? VersaoContrato,
    string? IdExecucao,
    string? IdSolicitacao,
    UsuarioSofia? Usuario,
    string? Pergunta,
    IReadOnlyList<DocumentoSofia>? Documentos,
    ContextoSofia? Contexto);

/// <summary>Usuário identificado pelo nome, só para contexto de negócio. Dado pessoal: não registrar em log.</summary>
public sealed record UsuarioSofia(string? Nome);

/// <summary>Documento já convertido em texto pela SOFIA. Conteúdo não confiável (Princípio X).</summary>
public sealed record DocumentoSofia(string? IdDocumento, string? Nome, string? Tipo, string? Texto);

public sealed record ContextoSofia(string? Idioma, string? Instrucoes);

/// <summary>Resposta do agente à SOFIA.</summary>
public sealed record ResultadoAnaliseSofia(
    string VersaoContrato,
    string IdExecucao,
    string IdSolicitacao,
    AgenteSofia Agente,
    string Status,
    string Analise,
    IReadOnlyList<AchadoSofia> Achados,
    IReadOnlyList<string>? Observacoes,
    ErroSofia? Erro)
{
    /// <summary>Resultado de negócio com status <c>erro</c>: a tarefa A2A conclui, mas a análise não foi possível.</summary>
    public static ResultadoAnaliseSofia Falha(string idExecucao, string idSolicitacao, AgenteSofia agente, string codigo, string mensagem) =>
        new(ContratoSofia.Versao, idExecucao, idSolicitacao, agente, StatusAnaliseSofia.Erro, mensagem, [], null, new ErroSofia(codigo, mensagem));
}

public sealed record AgenteSofia(string IdAgente, string Nome);

public sealed record AchadoSofia(
    string IdAchado,
    string Descricao,
    IReadOnlyList<string>? IdDocumentos,
    IReadOnlyList<string>? Evidencias,
    string? Severidade);

/// <summary>Erro de negócio não sensível (sem stack trace, conteúdo de documento ou nome do usuário).</summary>
public sealed record ErroSofia(string Codigo, string Mensagem);

public static class ContratoSofia
{
    public const string Versao = "1.0";

    /// <summary>Nome do Artifact A2A que carrega o <see cref="ResultadoAnaliseSofia"/>.</summary>
    public const string NomeArtefato = "analise-sofia-v1";
}

public static class StatusAnaliseSofia
{
    public const string Concluido = "concluido";
    public const string Parcial = "parcial";
    public const string Erro = "erro";

    public static readonly IReadOnlyList<string> Todos = [Concluido, Parcial, Erro];
}

public static class SeveridadeSofia
{
    public static readonly IReadOnlyList<string> Todas = ["baixa", "media", "alta", "critica"];
}

/// <summary>Identidade e limites do agente no contrato SOFIA (seção <c>Sofia</c> da configuração).</summary>
public sealed class OpcoesContratoSofia
{
    public string IdAgente { get; set; } = "agente-monitoramento-legislacao";

    public string NomeAgente { get; set; } = "Agente de Monitoramento da Legislação e Jurisprudência";

    public int MaxCaracteresPergunta { get; set; } = 4_000;

    public int MaxDocumentos { get; set; } = 20;

    public int MaxCaracteresPorDocumento { get; set; } = 200_000;

    public int MaxCaracteresTotalDocumentos { get; set; } = 500_000;

    public AgenteSofia Agente => new(IdAgente, NomeAgente);
}
