using Agente.Application.Comum;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Agendamento;

/// <summary>Coleta periódica: só dispara o caso de uso, com execução única entre instâncias (ADR 0003).</summary>
public sealed class AgendadorColeta(
    IServiceScopeFactory escopos,
    IBloqueioExecucao bloqueio,
    IOptions<OpcoesColeta> opcoes,
    ILogger<AgendadorColeta> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.Habilitada)
        {
            logger.LogInformation("Agendador de coleta desabilitado por configuração.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, opcoes.Value.IntervaloVerificacaoMinutos)));
        do
        {
            try
            {
                await using var trava = await bloqueio.TentarAdquirirAsync("agente.coleta", stoppingToken);
                if (trava is null)
                {
                    continue;
                }

                using var escopo = escopos.CreateScope();
                var ciclo = escopo.ServiceProvider.GetRequiredService<ExecutarCicloColeta>();
                var correlacao = $"coleta-{Guid.NewGuid():N}";
                var resultados = await ciclo.ExecutarAsync(correlacao, stoppingToken);
                logger.LogInformation(
                    "Ciclo de coleta {CorrelacaoId}: {Fontes} fonte(s), {Novas} novidade(s) nova(s), {Falhas} falha(s)",
                    correlacao, resultados.Count, resultados.Sum(r => r.NovasNovidades), resultados.Count(r => r.Status == StatusExecucao.FalhaTecnica));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha técnica no ciclo de coleta");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Expurgo diário de dados operacionais (FR-034) no horário Retencao:Horario.</summary>
public sealed class AgendadorRetencao(
    IServiceScopeFactory escopos,
    IBloqueioExecucao bloqueio,
    IRelogio relogio,
    IOptions<OpcoesRetencao> opcoes,
    ILogger<AgendadorRetencao> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var horario = TimeOnly.TryParse(opcoes.Value.Horario, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : new TimeOnly(2, 0);
        DateOnly? ultimaExecucao = null;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var agoraLocal = relogio.ParaLocal(relogio.AgoraUtc);
            var hoje = DateOnly.FromDateTime(agoraLocal.DateTime);
            if (ultimaExecucao == hoje || TimeOnly.FromDateTime(agoraLocal.DateTime) < horario)
            {
                continue;
            }

            try
            {
                await using var trava = await bloqueio.TentarAdquirirAsync("agente.retencao", stoppingToken);
                if (trava is null)
                {
                    continue;
                }

                using var escopo = escopos.CreateScope();
                await escopo.ServiceProvider.GetRequiredService<AplicarRetencao>().ExecutarAsync(stoppingToken);
                ultimaExecucao = hoje;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha técnica no expurgo de retenção");
            }
        }
    }
}
