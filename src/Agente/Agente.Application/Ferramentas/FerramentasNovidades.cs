using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConsultaAgregado = Agente.Domain.Operacao.Consulta;

namespace Agente.Application.Ferramentas;

/// <summary>O que as ferramentas devolveram numa conversa (para validar a resposta e montar o envelope).</summary>
public sealed class ColetorFerramentas
{
    private readonly ConcurrentDictionary<Guid, NovidadeDto> _novidades = new();
    private readonly ConcurrentBag<EnvelopeResposta> _envelopes = [];

    public IReadOnlyCollection<NovidadeDto> Novidades => _novidades.Values.ToList();

    public IReadOnlyCollection<EnvelopeResposta> Envelopes => _envelopes.ToList();

    public IReadOnlySet<string> UrlsRetornadas =>
        _novidades.Values.SelectMany(n => n.Evidencias.Select(e => e.Url)).ToHashSet(StringComparer.Ordinal);

    internal void Registrar(NovidadeDto n) => _novidades[n.Id] = n;

    internal void Registrar(EnvelopeResposta e)
    {
        _envelopes.Add(e);
        foreach (var n in e.Resultado?.Novidades ?? [])
        {
            Registrar(n);
        }
    }
}

/// <summary>
/// Ferramentas somente leitura expostas ao LLM (contracts/ferramentas.md). A autorização é checada
/// pelo sistema antes de executar; a escolha da ferramenta pelo LLM não autoriza nada (Princípio XII).
/// </summary>
public sealed class FerramentasNovidades(
    ConsultarNovidades consultar,
    IConsultaNovidades leitura,
    RegistradorEtapas etapas,
    IOptions<OpcoesFerramentas> opcoes,
    ILogger<FerramentasNovidades> logger)
{
    public const string BuscarNovidades = "buscar_novidades";
    public const string ObterNovidade = "obter_novidade";

    private static readonly string[] PapeisPermitidos = [Papeis.Consultor, Papeis.Desenvolvedor];

    public IReadOnlyList<DefinicaoFerramenta> Criar(
        ContextoUsuario usuario,
        string correlacaoId,
        string conversaId,
        ConsultaAgregado consultaPai,
        ColetorFerramentas coletor)
    {
        return
        [
            new DefinicaoFerramenta(
                BuscarNovidades,
                "Busca novidades de legislação e jurisprudência já coletadas das fontes oficiais do catálogo, por período e, opcionalmente, por assunto e filtros. Retorna novidades classificadas por esfera e perfil, com evidências.",
                [
                    new("assunto", "Assunto a pesquisar (2 a 200 caracteres). Opcional: omita para listar todas as novidades do período.", "string", false),
                    new("dataInicio", "Início do período (AAAA-MM-DD). Opcional.", "string", false),
                    new("dataFim", "Fim do período (AAAA-MM-DD). Opcional.", "string", false),
                    new("esferas", "Lista separada por vírgula: federal, estadual, municipal. Opcional.", "string", false),
                    new("perfis", "consultor_tributario (padrão, quando omitido) ou desenvolvedor (área de tecnologia). Opcional.", "string", false),
                    new("grupos", "Lista separada por vírgula: legislacao_federal, legislacao_estadual, legislacao_municipal, tributos, tribunais_judiciais, administrativos_contas. Opcional.", "string", false),
                ],
                (args, ct) => ExecutarComControle(BuscarNovidades, usuario, consultaPai, t => Buscar(args, usuario, correlacaoId, conversaId, coletor, t), ct)),
            new DefinicaoFerramenta(
                ObterNovidade,
                "Obtém o detalhe e as evidências de uma novidade pelo seu id.",
                [new("novidadeId", "Id (GUID) da novidade.", "string", true)],
                (args, ct) => ExecutarComControle(ObterNovidade, usuario, consultaPai, t => Obter(args, coletor, t), ct)),
        ];
    }

    private async Task<string> ExecutarComControle(
        string nome,
        ContextoUsuario usuario,
        ConsultaAgregado consultaPai,
        Func<CancellationToken, Task<string>> acao,
        CancellationToken ct)
    {
        var etapa = etapas.Iniciar(consultaPai, EtapaFluxo.Ferramentas);

        if (!usuario.TemAlgumPapel(PapeisPermitidos))
        {
            logger.LogWarning("Ferramenta {Ferramenta} recusada: usuário {Usuario} sem papel autorizado", nome, usuario.UsuarioId);
            etapas.Concluir(etapa, StatusEtapa.Falha, $"{nome}: não autorizado");
            return Erro("nao_autorizado", "Usuário sem permissão para esta ferramenta.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opcoes.Value.TimeoutSegundos)));
        try
        {
            var saida = await acao(timeout.Token);
            etapas.Concluir(etapa, StatusEtapa.Sucesso, nome);
            return saida;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            etapas.Concluir(etapa, StatusEtapa.Falha, $"{nome}: timeout");
            return Erro("falha_tecnica", "A ferramenta excedeu o tempo limite.");
        }
        catch (ArgumentException ex)
        {
            etapas.Concluir(etapa, StatusEtapa.Falha, $"{nome}: argumentos inválidos");
            return Erro("argumentos_invalidos", ex.Message);
        }
    }

    private async Task<string> Buscar(
        IReadOnlyDictionary<string, string?> args,
        ContextoUsuario usuario,
        string correlacaoId,
        string conversaId,
        ColetorFerramentas coletor,
        CancellationToken ct)
    {
        var esferas = ConsultarNovidades.LerLista<Esfera>(Lista(args, "esferas"), "esferas");
        var perfis = ConsultarNovidades.LerLista<Perfil>(Lista(args, "perfis"), "perfis");
        var grupos = ConsultarNovidades.LerLista<Grupo>(Lista(args, "grupos"), "grupos");
        var erros = esferas.Erros.Concat(perfis.Erros).Concat(grupos.Erros).ToList();
        if (erros.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", erros.Select(e => e.Mensagem)));
        }

        var pedido = new PedidoConsulta(
            Valor(args, "assunto") ?? string.Empty,
            Data(args, "dataInicio"),
            Data(args, "dataFim"),
            esferas.Valor,
            perfis.Valor,
            grupos.Valor);

        var resultado = await consultar.ExecutarAsync(pedido, usuario, correlacaoId, conversaId, ct);
        if (!resultado.Sucesso)
        {
            throw new ArgumentException(string.Join("; ", resultado.Erros.Select(e => e.Mensagem)));
        }

        coletor.Registrar(resultado.Valor);
        var r = resultado.Valor.Resultado!;
        return JsonSerializer.Serialize(new
        {
            r.Periodo,
            r.FontesComFalha,
            r.ItensOmitidos,
            r.MensagemSemNovidades,
            Novidades = r.Novidades.Select(Resumo),
            resultado.Valor.Limitacoes,
        }, JsonContrato.Opcoes);
    }

    private async Task<string> Obter(IReadOnlyDictionary<string, string?> args, ColetorFerramentas coletor, CancellationToken ct)
    {
        if (!Guid.TryParse(Valor(args, "novidadeId"), out var id))
        {
            throw new ArgumentException("'novidadeId' deve ser um GUID.");
        }

        var novidade = await leitura.ObterAsync(id, ct);
        if (novidade is null)
        {
            return Erro("nao_encontrada", "Novidade não encontrada.");
        }

        coletor.Registrar(novidade);
        return JsonSerializer.Serialize(Resumo(novidade), JsonContrato.Opcoes);
    }

    private static object Resumo(NovidadeDto n) => new
    {
        n.Id,
        n.Situacao,
        n.Esfera,
        n.Ente,
        n.Perfis,
        n.OrigemPerfil,
        n.DataPublicacao,
        n.OrgaoEmissor,
        n.TipoAto,
        n.NumeroAto,
        n.Ementa,
        n.Tema,
        n.Impacto,
        n.RequerRevisaoHumana,
        Links = n.Evidencias.Select(e => e.Url),
    };

    private static string Erro(string codigo, string mensagem) =>
        JsonSerializer.Serialize(new { Erro = codigo, Mensagem = mensagem }, JsonContrato.Opcoes);

    private static string? Valor(IReadOnlyDictionary<string, string?> args, string nome) =>
        args.TryGetValue(nome, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static string[] Lista(IReadOnlyDictionary<string, string?> args, string nome) =>
        Valor(args, nome)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private static DateOnly? Data(IReadOnlyDictionary<string, string?> args, string nome)
    {
        var v = Valor(args, nome);
        if (v is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new ArgumentException($"'{nome}' deve estar no formato AAAA-MM-DD.");
    }
}
