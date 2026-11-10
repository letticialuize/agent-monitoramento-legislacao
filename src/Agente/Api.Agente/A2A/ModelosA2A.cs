using System.Text.Json;

namespace Api.Agente.A2A;

// Subconjunto do A2A v0.3.x (JSON-RPC 2.0) usado com a SOFIA: message/send síncrono, partes de texto e
// Task com Artifact. Campos do protocolo, não do contrato de negócio. Sem SDK A2A instalado no projeto.

public static class ProtocoloA2A
{
    public const string Versao = "0.3.0";
    public const string Transporte = "JSONRPC";
    public const string MetodoEnviarMensagem = "message/send";
    public const string MetodoObterTarefa = "tasks/get";
    public const string MetodoCancelarTarefa = "tasks/cancel";

    /// <summary>Serialização do envelope A2A: a do contrato, sem campos nulos opcionais do protocolo.</summary>
    public static JsonSerializerOptions Json { get; } = CriarJson();

    private static JsonSerializerOptions CriarJson()
    {
        var o = new JsonSerializerOptions(global::Agente.Application.Comum.JsonContrato.Opcoes)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        o.MakeReadOnly();
        return o;
    }
}

public sealed record RequisicaoJsonRpc(string? Jsonrpc, JsonElement? Id, string? Method, ParametrosRpcA2A? Params);

/// <summary><c>message</c> em <c>message/send</c>; <c>id</c> (da Task) em <c>tasks/get</c> e <c>tasks/cancel</c>.</summary>
public sealed record ParametrosRpcA2A(MensagemA2A? Message, string? Id = null);

public sealed record RespostaJsonRpc(string Jsonrpc, JsonElement? Id, TarefaA2A? Result, ErroJsonRpc? Error);

public sealed record ErroJsonRpc(int Code, string Message);

public sealed record MensagemA2A(
    string? Role,
    string? MessageId,
    IReadOnlyList<ParteA2A>? Parts,
    string? ContextId = null,
    string? TaskId = null,
    string Kind = "message");

/// <summary>Parte de mensagem ou artefato. Só <c>kind = text</c> carrega o contrato SOFIA.</summary>
public sealed record ParteA2A(string? Kind, string? Text);

public sealed record TarefaA2A(
    string Id,
    string ContextId,
    StatusTarefaA2A Status,
    IReadOnlyList<ArtefatoA2A>? Artifacts,
    string Kind = "task");

public sealed record StatusTarefaA2A(string State, MensagemA2A? Message = null, DateTimeOffset? Timestamp = null);

public sealed record ArtefatoA2A(string ArtifactId, string? Name, IReadOnlyList<ParteA2A> Parts);

public static class EstadoTarefaA2A
{
    public const string Enviada = "submitted";
    public const string EmAndamento = "working";
    public const string Concluida = "completed";
    public const string Falhou = "failed";
}
