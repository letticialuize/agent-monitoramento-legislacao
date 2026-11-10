using System.Diagnostics;
using System.Text.Json;
using Agente.Application.Portas;
using Agente.Application.Sofia;
using Api.Agente.Observabilidade;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A;

/// <summary>
/// Servidor A2A v0.3 (JSON-RPC 2.0) para a SOFIA (features 005 e 006). Exige o Bearer da SOFIA antes de
/// ler o corpo. <c>message/send</c> é síncrono e devolve sempre uma Task terminal (<c>completed</c> com o
/// artefato do contrato, ou <c>failed</c>), guardada para <c>tasks/get</c>. Como nenhuma Task fica em
/// andamento, <c>tasks/cancel</c> responde TaskNotCancelable. Erros de protocolo e de contrato voltam
/// como erro JSON-RPC, com HTTP 200, sem ecoar o conteúdo recebido.
/// </summary>
public static class A2AEndpoints
{
    // Códigos JSON-RPC 2.0 e A2A v0.3.
    public const int ErroParse = -32700;
    public const int RequisicaoInvalida = -32600;
    public const int MetodoInexistente = -32601;
    public const int ParametrosInvalidos = -32602;
    public const int TarefaInexistente = -32001;
    public const int TarefaNaoCancelavel = -32002;

    public static IEndpointRouteBuilder MapA2A(this IEndpointRouteBuilder app)
    {
        app.MapPost(AgentCardSofia.RotaA2A, Tratar)
            .RequireTokenSofia()
            .WithName("A2AJsonRpc")
            .WithTags("A2A")
            .WithSummary("Endpoint A2A v0.3 (JSON-RPC 2.0: message/send, tasks/get, tasks/cancel) chamado pela SOFIA")
            .WithDescription("Exige Authorization: Bearer com a chave SOFIA_A2A_TOKEN. message/send recebe SolicitacaoAnaliseSofia v1.0 em uma TextPart e devolve uma Task com o artefato analise-sofia-v1. Veja specs/004-contrato-sofia-a2a/contracts/exemplos.")
            .Accepts<RequisicaoJsonRpc>("application/json")
            .Produces<RespostaJsonRpc>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> Tratar(
        HttpContext http,
        MapeadorA2ASofia mapeador,
        AnalisarSolicitacaoSofia handler,
        IRepositorioTarefasA2A tarefas,
        IOptions<OpcoesA2A> opcoes,
        ILoggerFactory logs)
    {
        var logger = logs.CreateLogger("Api.Agente.A2A");
        RequisicaoJsonRpc? rpc;
        try
        {
            rpc = await JsonSerializer.DeserializeAsync<RequisicaoJsonRpc>(http.Request.Body, ProtocoloA2A.Json, http.RequestAborted);
        }
        catch (JsonException)
        {
            return Erro(null, ErroParse, "Corpo não é JSON válido.");
        }

        if (rpc is null || rpc.Jsonrpc != "2.0" || string.IsNullOrWhiteSpace(rpc.Method))
        {
            return Erro(rpc?.Id, RequisicaoInvalida, "Requisição JSON-RPC 2.0 inválida.");
        }

        return rpc.Method switch
        {
            ProtocoloA2A.MetodoEnviarMensagem => await EnviarMensagem(http, rpc, mapeador, handler, tarefas, opcoes.Value, logger),
            ProtocoloA2A.MetodoObterTarefa => await ObterTarefa(rpc, tarefas, http.RequestAborted),
            ProtocoloA2A.MetodoCancelarTarefa => await CancelarTarefa(rpc, tarefas, http.RequestAborted),
            _ => Erro(rpc.Id, MetodoInexistente, $"Método não suportado. Use {ProtocoloA2A.MetodoEnviarMensagem}, {ProtocoloA2A.MetodoObterTarefa} ou {ProtocoloA2A.MetodoCancelarTarefa}."),
        };
    }

    private static async Task<IResult> EnviarMensagem(
        HttpContext http,
        RequisicaoJsonRpc rpc,
        MapeadorA2ASofia mapeador,
        AnalisarSolicitacaoSofia handler,
        IRepositorioTarefasA2A tarefas,
        OpcoesA2A opcoes,
        ILogger logger)
    {
        var mensagem = rpc.Params?.Message;
        if (mensagem is null || mensagem.Role != "user" || string.IsNullOrWhiteSpace(mensagem.MessageId))
        {
            return Erro(rpc.Id, ParametrosInvalidos, "params.message exige role \"user\" e messageId.");
        }

        var solicitacao = mapeador.LerSolicitacao(mensagem);
        if (!solicitacao.Sucesso)
        {
            // As mensagens do validador não repetem nome do usuário nem texto de documento.
            var detalhes = string.Join(" ", solicitacao.Erros.Select(e => $"[{e.Codigo}] {e.Mensagem}"));
            logger.LogInformation("A2A: solicitação recusada ({Codigos}).", string.Join(",", solicitacao.Erros.Select(e => e.Codigo).Distinct()));
            return Erro(rpc.Id, ParametrosInvalidos, $"Solicitação fora do contrato SOFIA v{ContratoSofia.Versao}. {detalhes}");
        }

        var s = solicitacao.Valor;
        var idTarefa = Guid.NewGuid().ToString();
        var idContexto = string.IsNullOrWhiteSpace(mensagem.ContextId) ? s.IdExecucao! : mensagem.ContextId;
        var cronometro = Stopwatch.StartNew();

        TarefaA2A tarefa;
        using (var limite = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted))
        {
            limite.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opcoes.TimeoutSegundos)));
            try
            {
                var resultado = await handler.ExecutarAsync(s, http.CorrelacaoId(), limite.Token);
                var criada = mapeador.CriarTarefaConcluida(resultado, s, idTarefa, idContexto);
                if (criada.Sucesso)
                {
                    tarefa = criada.Valor;
                    logger.LogInformation("A2A {IdExecucao}/{IdSolicitacao}: tarefa {IdTarefa} completed ({Status}) em {Ms} ms.",
                        s.IdExecucao, s.IdSolicitacao, idTarefa, resultado.Status, cronometro.ElapsedMilliseconds);
                }
                else
                {
                    logger.LogError("A2A {IdExecucao}/{IdSolicitacao}: resultado fora do contrato ({Codigos}).",
                        s.IdExecucao, s.IdSolicitacao, string.Join(",", criada.Erros.Select(e => e.Codigo).Distinct()));
                    tarefa = Falhou(idTarefa, idContexto, "O agente produziu um resultado fora do contrato.");
                }
            }
            catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning("A2A {IdExecucao}/{IdSolicitacao}: tempo limite de {Segundos} s.", s.IdExecucao, s.IdSolicitacao, opcoes.TimeoutSegundos);
                tarefa = Falhou(idTarefa, idContexto, "Tempo limite da análise esgotado.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha grave: Task failed sem detalhes internos (a exceção fica só no log).
                logger.LogError(ex, "A2A {IdExecucao}/{IdSolicitacao}: falha inesperada.", s.IdExecucao, s.IdSolicitacao);
                tarefa = Falhou(idTarefa, idContexto, "Falha interna do agente.");
            }
        }

        await Guardar(tarefas, tarefa, logger);
        return Ok(rpc.Id, tarefa);
    }

    private static async Task<IResult> ObterTarefa(RequisicaoJsonRpc rpc, IRepositorioTarefasA2A tarefas, CancellationToken ct)
    {
        var id = rpc.Params?.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            return Erro(rpc.Id, ParametrosInvalidos, "params.id (id da Task) é obrigatório.");
        }

        var json = await tarefas.ObterAsync(id, ct);
        return json is null
            ? Erro(rpc.Id, TarefaInexistente, "Task não encontrada.")
            : Ok(rpc.Id, JsonSerializer.Deserialize<TarefaA2A>(json, ProtocoloA2A.Json)!);
    }

    private static async Task<IResult> CancelarTarefa(RequisicaoJsonRpc rpc, IRepositorioTarefasA2A tarefas, CancellationToken ct)
    {
        var id = rpc.Params?.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            return Erro(rpc.Id, ParametrosInvalidos, "params.id (id da Task) é obrigatório.");
        }

        // message/send é síncrono: toda Task conhecida já está em estado terminal.
        return await tarefas.ObterAsync(id, ct) is null
            ? Erro(rpc.Id, TarefaInexistente, "Task não encontrada.")
            : Erro(rpc.Id, TarefaNaoCancelavel, "Task já finalizada; não pode ser cancelada.");
    }

    private static async Task Guardar(IRepositorioTarefasA2A tarefas, TarefaA2A tarefa, ILogger logger)
    {
        try
        {
            // Sem o token da requisição: a análise já terminou e a Task deve ficar disponível para tasks/get.
            await tarefas.SalvarAsync(tarefa.Id, JsonSerializer.Serialize(tarefa, ProtocoloA2A.Json), CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A resposta segue: só tasks/get dessa Task fica indisponível.
            logger.LogError(ex, "A2A: não foi possível guardar a tarefa {IdTarefa}.", tarefa.Id);
        }
    }

    private static TarefaA2A Falhou(string idTarefa, string idContexto, string motivo) =>
        new(idTarefa, idContexto,
            new StatusTarefaA2A(EstadoTarefaA2A.Falhou, new MensagemA2A("agent", Guid.NewGuid().ToString(), [new ParteA2A("text", motivo)], idContexto, idTarefa)),
            null);

    private static IResult Ok(JsonElement? id, TarefaA2A tarefa) =>
        Results.Json(new RespostaJsonRpc("2.0", id, tarefa, null), ProtocoloA2A.Json);

    private static IResult Erro(JsonElement? id, int codigo, string mensagem) =>
        Results.Json(new RespostaJsonRpc("2.0", id, null, new ErroJsonRpc(codigo, mensagem)), ProtocoloA2A.Json);
}
