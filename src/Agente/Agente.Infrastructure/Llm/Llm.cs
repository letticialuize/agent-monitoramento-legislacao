using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Agente.Application.Portas;
using Agente.Domain.Operacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;
using ChatTokenUsage = OpenAI.Chat.ChatTokenUsage;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace Agente.Infrastructure.Llm;

/// <summary>Configuração externa do LLM (Princípio XI). A chave vem do provedor de segredos.</summary>
public sealed class OpcoesLlm
{
    public string Provedor { get; set; } = "openai";

    public string Modelo { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Opcional: endpoint compatível (ex.: simulador nos testes).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public float Temperatura { get; set; }

    public int MaxTokensSaida { get; set; } = 4000;

    public int TimeoutSegundos { get; set; } = 60;

    public int TentativasTransitorias { get; set; } = 3;

    public int MaxIteracoesFerramentas { get; set; } = 5;

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(Modelo) && Modelo != "definir-por-ambiente" && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Cria o serviço de chat do Semantic Kernel sem retentativas ocultas do SDK (cada chamada é registrada).</summary>
internal sealed class FabricaChatOpenAi(IOptions<OpcoesLlm> opcoes)
{
    private readonly Lazy<IChatCompletionService> _servico = new(() => Criar(opcoes.Value));

    public IChatCompletionService Servico => _servico.Value;

    private static IChatCompletionService Criar(OpcoesLlm o)
    {
        var clienteOpcoes = new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = TimeSpan.FromSeconds(Math.Max(5, o.TimeoutSegundos)),
        };
        if (!string.IsNullOrWhiteSpace(o.Endpoint))
        {
            clienteOpcoes.Endpoint = new Uri(o.Endpoint);
        }

        var cliente = new OpenAIClient(new ApiKeyCredential(o.ApiKey), clienteOpcoes);
        return new OpenAIChatCompletionService(o.Modelo, cliente);
    }
}

internal static class UsoLlm
{
    public static (int? Entrada, int? Saida) Extrair(ChatMessageContent? resposta)
    {
        if (resposta?.Metadata is not null &&
            resposta.Metadata.TryGetValue("Usage", out var uso) &&
            uso is ChatTokenUsage u)
        {
            return (u.InputTokenCount, u.OutputTokenCount);
        }

        return (null, null);
    }

    public static bool Transitorio(Exception ex) => ex switch
    {
        HttpOperationException h => h.StatusCode is null || (int)h.StatusCode == 429 || (int)h.StatusCode >= 500,
        ClientResultException c => c.Status == 0 || c.Status == 429 || c.Status >= 500,
        TaskCanceledException or TimeoutException or HttpRequestException => true,
        _ => false,
    };
}

/// <summary>Saída estruturada (Structured Outputs com JSON Schema estrito) via Semantic Kernel.</summary>
internal sealed class ClienteLlmSemanticKernel(
    IServiceProvider servicos,
    IRegistradorConsumo consumo,
    IRelogio relogio,
    IOptions<OpcoesLlm> opcoes,
    ILogger<ClienteLlmSemanticKernel> logger) : IClienteLlmEstruturado
{
    public bool Configurado => opcoes.Value.Configurado;

    public async Task<RespostaLlm> GerarJsonAsync(RequisicaoLlm requisicao, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        var o = opcoes.Value;
        if (!o.Configurado)
        {
            return new RespostaLlm(false, null, "LLM não configurado.", o.Modelo);
        }

        var chat = servicos.GetRequiredService<FabricaChatOpenAi>().Servico;
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = o.Temperatura,
            MaxTokens = o.MaxTokensSaida,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                requisicao.NomeSchema.Replace('-', '_'),
                BinaryData.FromString(requisicao.SchemaEstrito),
                jsonSchemaIsStrict: true),
        };

        var historico = new ChatHistory(requisicao.PromptSistema);
        historico.AddUserMessage(requisicao.MensagemUsuario);

        var maximo = Math.Max(1, o.TentativasTransitorias);
        string? erro = null;
        for (var tentativa = 1; tentativa <= maximo; tentativa++)
        {
            var inicio = relogio.AgoraUtc;
            var cronometro = Stopwatch.StartNew();
            try
            {
                var resposta = await chat.GetChatMessageContentAsync(historico, settings, cancellationToken: ct);
                var (entrada, saida) = UsoLlm.Extrair(resposta);
                await Registrar(requisicao, o, NumeroTentativa(requisicao, tentativa), entrada, saida, inicio, cronometro.Elapsed, "sucesso", ct);
                return new RespostaLlm(true, resposta.Content, null, o.Modelo);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                erro = $"{ex.GetType().Name}: {ex.Message}";
                await Registrar(requisicao, o, NumeroTentativa(requisicao, tentativa), null, null, inicio, cronometro.Elapsed, "falha", ct);
                if (!UsoLlm.Transitorio(ex) || tentativa == maximo)
                {
                    break;
                }

                logger.LogWarning("Erro transitório no LLM (tentativa {Tentativa}/{Max}): {Erro}", tentativa, maximo, erro);
                await Task.Delay(Espera(tentativa), ct);
            }
        }

        return new RespostaLlm(false, null, erro, o.Modelo);
    }

    private static int NumeroTentativa(RequisicaoLlm r, int tentativaRede) => ((r.TentativaValidacao - 1) * 100) + tentativaRede;

    internal static TimeSpan Espera(int tentativa) =>
        TimeSpan.FromMilliseconds((Math.Pow(2, tentativa) * 250) + Random.Shared.Next(0, 250));

    private Task Registrar(RequisicaoLlm r, OpcoesLlm o, int tentativa, int? entrada, int? saida, DateTimeOffset inicio, TimeSpan duracao, string status, CancellationToken ct) =>
        consumo.RegistrarAsync(new DadosConsumo(r.Contexto, o.Provedor, o.Modelo, r.Finalidade, tentativa, entrada, saida, inicio, duracao, status), ct);
}

/// <summary>
/// Conversa com Function Calling em laço manual (sem invocação automática): cada chamada ao modelo
/// é registrada e cada ferramenta é executada pelo sistema, nunca pelo SDK.
/// </summary>
internal sealed class OrquestradorConversaSemanticKernel(
    IServiceProvider servicos,
    IRegistradorConsumo consumo,
    IRelogio relogio,
    IOptions<OpcoesLlm> opcoes,
    ILogger<OrquestradorConversaSemanticKernel> logger) : IOrquestradorConversa
{
    private const string Plugin = "novidades";

    public bool Configurado => opcoes.Value.Configurado;

    public async Task<RespostaConversa> ResponderAsync(RequisicaoConversa requisicao, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        var o = opcoes.Value;
        var chat = servicos.GetRequiredService<FabricaChatOpenAi>().Servico;

        var kernel = new Kernel();
        kernel.Plugins.AddFromFunctions(Plugin, requisicao.Ferramentas.Select(Funcao));
        var porNome = requisicao.Ferramentas.ToDictionary(f => f.Nome, StringComparer.Ordinal);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = o.Temperatura,
            MaxTokens = o.MaxTokensSaida,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false),
        };

        var historico = new ChatHistory(requisicao.PromptSistema);
        foreach (var m in requisicao.Historico)
        {
            historico.AddMessage(m.Papel == "assistente" ? AuthorRole.Assistant : AuthorRole.User, m.Texto);
        }

        historico.AddUserMessage(requisicao.MensagemUsuario);

        var chamada = 0;
        for (var iteracao = 1; iteracao <= Math.Max(1, o.MaxIteracoesFerramentas); iteracao++)
        {
            ChatMessageContent resposta;
            var inicio = relogio.AgoraUtc;
            var cronometro = Stopwatch.StartNew();
            chamada++;
            try
            {
                resposta = await chat.GetChatMessageContentAsync(historico, settings, kernel, ct);
                var (entrada, saida) = UsoLlm.Extrair(resposta);
                await Registrar(requisicao, o, chamada, entrada, saida, inicio, cronometro.Elapsed, "sucesso", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                await Registrar(requisicao, o, chamada, null, null, inicio, cronometro.Elapsed, "falha", ct);
                logger.LogWarning(ex, "Falha na chamada ao LLM (execução {ExecucaoId})", requisicao.Contexto.ExecucaoId);
                return new RespostaConversa(false, null, $"{ex.GetType().Name}: {ex.Message}");
            }

            historico.Add(resposta);
            var chamadas = FunctionCallContent.GetFunctionCalls(resposta).ToList();
            if (chamadas.Count == 0)
            {
                return new RespostaConversa(true, resposta.Content, null);
            }

            foreach (var fc in chamadas)
            {
                string saidaFerramenta;
                if (!porNome.TryGetValue(fc.FunctionName, out var definicao))
                {
                    saidaFerramenta = JsonSerializer.Serialize(new { erro = "ferramenta_inexistente", nome = fc.FunctionName });
                }
                else
                {
                    var args = (fc.Arguments ?? new KernelArguments())
                        .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString(), StringComparer.Ordinal);
                    saidaFerramenta = await definicao.Executar(args, ct);
                }

                historico.Add(new FunctionResultContent(fc, saidaFerramenta).ToChatMessage());
            }
        }

        return new RespostaConversa(false, null, "Limite de iterações de ferramentas atingido.");
    }

    private static KernelFunction Funcao(DefinicaoFerramenta d) =>
        KernelFunctionFactory.CreateFromMethod(
            static () => string.Empty,
            d.Nome,
            d.Descricao,
            d.Parametros.Select(p => new KernelParameterMetadata(p.Nome)
            {
                Description = p.Descricao,
                IsRequired = p.Obrigatorio,
                ParameterType = typeof(string),
            }),
            new KernelReturnParameterMetadata { ParameterType = typeof(string) });

    private Task Registrar(RequisicaoConversa r, OpcoesLlm o, int tentativa, int? entrada, int? saida, DateTimeOffset inicio, TimeSpan duracao, string status, CancellationToken ct) =>
        consumo.RegistrarAsync(new DadosConsumo(r.Contexto, o.Provedor, o.Modelo, FinalidadeLlm.Conversa, tentativa, entrada, saida, inicio, duracao, status), ct);
}
