using System.Text.Json;
using Agente.Application.Comum;
using Agente.Application.Sofia;
using Agente.Domain.Comum;

namespace Api.Agente.A2A;

/// <summary>
/// Converte entre o envelope A2A v0.3 e o contrato SOFIA v1.0. A2A → TextPart → <see cref="SolicitacaoAnaliseSofia"/>;
/// <see cref="ResultadoAnaliseSofia"/> → TextPart → Artifact de Task <c>completed</c>.
/// Campos novos e desconhecidos do contrato são ignorados (evolução compatível da v1).
/// </summary>
public sealed class MapeadorA2ASofia(ValidadorContratoSofia validador)
{
    public Resultado<SolicitacaoAnaliseSofia> LerSolicitacao(MensagemA2A? mensagem)
    {
        if (mensagem is null)
        {
            return Resultado<SolicitacaoAnaliseSofia>.Falha("a2a.mensagem", "params.message é obrigatório.");
        }

        var textos = (mensagem.Parts ?? []).Where(p => p?.Kind == "text" && !string.IsNullOrWhiteSpace(p.Text)).ToList();
        if (textos.Count != 1)
        {
            return Resultado<SolicitacaoAnaliseSofia>.Falha("a2a.partes", "A mensagem deve ter exatamente uma parte de texto com o JSON do contrato SOFIA.");
        }

        var lido = Desserializar<SolicitacaoAnaliseSofia>(textos[0].Text!);
        return lido.Sucesso ? validador.ValidarSolicitacao(lido.Valor) : Resultado<SolicitacaoAnaliseSofia>.Falha(lido.Erros);
    }

    /// <summary>Monta a Task concluída com o resultado já validado contra a solicitação.</summary>
    public Resultado<TarefaA2A> CriarTarefaConcluida(ResultadoAnaliseSofia resultado, SolicitacaoAnaliseSofia solicitacao, string idTarefa, string idContexto)
    {
        var validacao = validador.ValidarResultado(resultado, solicitacao);
        if (!validacao.Sucesso)
        {
            return Resultado<TarefaA2A>.Falha(validacao.Erros);
        }

        var artefato = new ArtefatoA2A(
            $"{idTarefa}-{ContratoSofia.NomeArtefato}",
            ContratoSofia.NomeArtefato,
            [new ParteA2A("text", JsonSerializer.Serialize(resultado, JsonContrato.Opcoes))]);

        return Resultado<TarefaA2A>.Ok(new TarefaA2A(idTarefa, idContexto, new StatusTarefaA2A(EstadoTarefaA2A.Concluida), [artefato]));
    }

    /// <summary>
    /// Lado do consumidor (SOFIA e testes): só uma Task <c>completed</c> com o artefato do contrato tem
    /// resultado. Task em andamento ou <c>failed</c> é tratada como falha técnica, não como análise.
    /// </summary>
    public static Resultado<ResultadoAnaliseSofia> LerResultado(TarefaA2A? tarefa)
    {
        if (tarefa?.Status?.State != EstadoTarefaA2A.Concluida)
        {
            return Resultado<ResultadoAnaliseSofia>.Falha("a2a.tarefa", $"A tarefa não está concluída (estado: {tarefa?.Status?.State ?? "ausente"}).");
        }

        var texto = (tarefa.Artifacts ?? [])
            .Where(a => a.Name == ContratoSofia.NomeArtefato)
            .SelectMany(a => a.Parts)
            .FirstOrDefault(p => p.Kind == "text")?.Text;

        return texto is null
            ? Resultado<ResultadoAnaliseSofia>.Falha("a2a.artefato", $"Artefato {ContratoSofia.NomeArtefato} ausente.")
            : Desserializar<ResultadoAnaliseSofia>(texto);
    }

    private static Resultado<T> Desserializar<T>(string json)
        where T : class
    {
        try
        {
            var valor = JsonSerializer.Deserialize<T>(json, JsonContrato.Opcoes);
            return valor is null ? Resultado<T>.Falha("contrato.json", "JSON do contrato vazio.") : Resultado<T>.Ok(valor);
        }
        catch (JsonException)
        {
            // Não devolve a exceção: a mensagem pode repetir trechos do documento.
            return Resultado<T>.Falha("contrato.json", "O texto da parte não é um JSON válido do contrato SOFIA.");
        }
    }
}
