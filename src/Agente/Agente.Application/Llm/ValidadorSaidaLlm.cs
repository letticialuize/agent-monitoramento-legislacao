using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Application.Comum;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agente.Application.Llm;

/// <summary>
/// Chama o LLM com saída estruturada e só aceita a resposta após validação de schema e validação
/// semântica (Princípio XI). Em caso de resposta inválida, faz até N retentativas reenviando o erro;
/// esgotadas, devolve falha técnica sem lançar exceção (research R-06).
/// </summary>
public sealed class ValidadorSaidaLlm(
    IClienteLlmEstruturado cliente,
    IOptions<OpcoesValidacaoLlm> opcoes,
    ILogger<ValidadorSaidaLlm> logger)
{
    private static readonly EvaluationOptions OpcoesAvaliacao = new() { OutputFormat = OutputFormat.List };

    public bool Disponivel => cliente.Configurado;

    /// <param name="validacaoSemantica">Devolve a lista de violações semânticas (vazia = válido).</param>
    public async Task<Resultado<JsonNode>> GerarAsync(
        ContextoExecucao contexto,
        FinalidadeLlm finalidade,
        ContratoLlm contrato,
        string promptSistema,
        string mensagemUsuario,
        Func<JsonNode, IReadOnlyList<string>> validacaoSemantica,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contrato);
        ArgumentNullException.ThrowIfNull(validacaoSemantica);

        var maxTentativas = 1 + Math.Max(0, opcoes.Value.RetentativasValidacao);
        var mensagem = mensagemUsuario;
        string? ultimoErro = null;

        for (var tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            var resposta = await cliente.GerarJsonAsync(
                new RequisicaoLlm(contexto, finalidade, promptSistema, mensagem, contrato.Nome, contrato.SchemaEstrito, tentativa),
                ct);

            if (!resposta.Sucesso || string.IsNullOrWhiteSpace(resposta.Json))
            {
                // Falha de comunicação já esgotou as tentativas transitórias do cliente.
                return Resultado<JsonNode>.Falha("llm.indisponivel", resposta.Erro ?? "Resposta vazia do LLM.");
            }

            var erros = Validar(contrato, resposta.Json, validacaoSemantica, out var no);
            if (erros.Count == 0)
            {
                return Resultado<JsonNode>.Ok(no!);
            }

            ultimoErro = string.Join("; ", erros);
            logger.LogWarning(
                "Saída do LLM inválida ({Contrato}, tentativa {Tentativa}/{Max}, execução {ExecucaoId}): {Erros}",
                contrato.Nome, tentativa, maxTentativas, contexto.ExecucaoId, ultimoErro);

            mensagem = mensagemUsuario +
                "\n\nA resposta anterior foi rejeitada pela validação pelos motivos abaixo. Corrija e responda novamente seguindo o schema:\n" +
                ultimoErro;
        }

        return Resultado<JsonNode>.Falha("llm.saida_invalida", $"Saída inválida após {maxTentativas} tentativas: {ultimoErro}");
    }

    internal static List<string> Validar(ContratoLlm contrato, string json, Func<JsonNode, IReadOnlyList<string>> semantica, out JsonNode? no)
    {
        no = null;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return [$"JSON malformado: {ex.Message}"];
        }

        if (parsed is null)
        {
            return ["JSON vazio."];
        }

        var avaliacao = contrato.Schema.Evaluate(JsonDocument.Parse(json).RootElement, OpcoesAvaliacao);
        if (!avaliacao.IsValid)
        {
            var detalhes = (avaliacao.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
                .Distinct()
                .Take(10)
                .ToList();
            return detalhes.Count > 0 ? detalhes : ["Resposta não atende ao schema."];
        }

        var semanticos = semantica(parsed).ToList();
        if (semanticos.Count == 0)
        {
            no = parsed;
        }

        return semanticos;
    }
}
