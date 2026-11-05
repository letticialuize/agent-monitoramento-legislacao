using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Portas;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agente.Application.Operacao;

/// <summary>Registra as etapas do fluxo obrigatório (Princípio VI) no agregado <see cref="Consulta"/>.</summary>
public sealed class RegistradorEtapas(IRelogio relogio)
{
    public EtapaExecucao Iniciar(Consulta consulta, EtapaFluxo etapa)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return consulta.IniciarEtapa(etapa, relogio.AgoraUtc);
    }

    public void Concluir(EtapaExecucao etapa, StatusEtapa status, string? detalhe = null)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        etapa.Finalizar(status, detalhe, relogio.AgoraUtc);
    }

    public void NaoExecutada(Consulta consulta, EtapaFluxo etapa, string motivo) =>
        Concluir(Iniciar(consulta, etapa), StatusEtapa.NaoExecutada, motivo);
}

/// <summary>Resumo do consumo registrado para uma execução.</summary>
public interface IConsultaConsumo
{
    Task<ResumoConsumo> ResumirAsync(Guid execucaoId, CancellationToken ct);
}

/// <summary>Exclui dados operacionais com mais de Retencao:Dias (FR-034, constitution 4.3).</summary>
public sealed class AplicarRetencao(
    IExpurgoOperacional expurgo,
    IRelogio relogio,
    IOptions<OpcoesRetencao> opcoes,
    ILogger<AplicarRetencao> logger)
{
    public async Task<ResultadoExpurgo> ExecutarAsync(CancellationToken ct)
    {
        var dias = Math.Max(1, opcoes.Value.Dias);
        var limite = relogio.AgoraUtc.AddDays(-dias);
        var resultado = await expurgo.ExpurgarAsync(limite, ct);
        logger.LogInformation(
            "Retenção aplicada (limite {Limite:o}): {Removidos}",
            limite,
            string.Join(", ", resultado.RemovidosPorTabela.Select(kv => $"{kv.Key}={kv.Value}")));
        return resultado;
    }
}
