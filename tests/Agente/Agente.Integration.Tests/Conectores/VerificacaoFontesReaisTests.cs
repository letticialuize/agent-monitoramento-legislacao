using System.Text;
using System.Text.Json;
using Agente.Application.Comum;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Infrastructure.Conectores;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Integration.Tests.Conectores;

/// <summary>Só roda com FONTES_REAIS=1: acessa os sites reais e não faz parte da suíte automática.</summary>
public sealed class FatoFontesReaisAttribute : FactAttribute
{
    public FatoFontesReaisAttribute()
    {
        if (Environment.GetEnvironmentVariable("FONTES_REAIS") != "1")
        {
            Skip = "Verificação manual: defina FONTES_REAIS=1 (requer internet).";
        }
    }
}

/// <summary>
/// Roda os conectores reais contra o catálogo real e grava um relatório por fonte.
/// Serve para validar seletores e detectar páginas que mudaram.
/// </summary>
public sealed class VerificacaoFontesReaisTests
{
    [FatoFontesReais]
    public async Task Relatorio_das_fontes_reais()
    {
        var servicos = new ServiceCollection();
        servicos.AddHttpClient(Ajudas.ClienteHttp, c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("infis-agente-monitoramento-legislacao/1.0");
            c.Timeout = TimeSpan.FromSeconds(40);
        });
        await using var provedor = servicos.BuildServiceProvider();
        var http = provedor.GetRequiredService<IHttpClientFactory>();
        var conectores = new Dictionary<string, (TipoConector Tipo, IConectorFonte Conector)>
        {
            ["PaginaOficial"] = (TipoConector.PaginaOficial, new PaginaOficialConector(http)),
            ["SenadoDadosAbertos"] = (TipoConector.SenadoDadosAbertos, new SenadoConector(http)),
            ["CamaraDadosAbertos"] = (TipoConector.CamaraDadosAbertos, new CamaraConector(http)),
            ["Djen"] = (TipoConector.Djen, new DjenConector(http)),
            ["QueridoDiario"] = (TipoConector.QueridoDiario, new QueridoDiarioConector(http)),
        };

        var caminho = Environment.GetEnvironmentVariable("FONTES_REAIS_CATALOGO")
            ?? throw new InvalidOperationException("Defina FONTES_REAIS_CATALOGO com o caminho do catálogo.");
        using var catalogo = JsonDocument.Parse(await File.ReadAllTextAsync(caminho));

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var dias = int.TryParse(Environment.GetEnvironmentVariable("FONTES_REAIS_DIAS"), out var d) ? d : 120;
        var relatorio = new StringBuilder();
        foreach (var f in catalogo.RootElement.GetProperty("fontes").EnumerateArray())
        {
            if (!conectores.TryGetValue(f.GetProperty("tipoConector").GetString()!, out var alvo))
            {
                continue;
            }

            var codigo = f.GetProperty("codigo").GetString()!;
            var cfg = f.TryGetProperty("configuracaoConector", out var c) ? c.GetRawText() : null;
            var fonte = new FonteParaColeta(Guid.NewGuid(), codigo, codigo, f.GetProperty("linkOficial").GetString()!, f.GetProperty("orgaoEmissor").GetString()!, NaturezaFonte.Oficial, alvo.Tipo, cfg);
            try
            {
                // O Senado faz uma chamada por dia da janela: limitada a 15 dias aqui.
                var janela = alvo.Tipo == TipoConector.SenadoDadosAbertos ? Math.Min(dias, 15) : dias;
                var r = await alvo.Conector.ColetarAsync(fonte, hoje.AddDays(-janela), hoje, CancellationToken.None);
                relatorio.AppendLine($"OK   {codigo}: {r.Itens.Count} item(ns); parcial={r.Parcial}; aviso={r.Aviso}");
                foreach (var i in r.Itens.OrderByDescending(x => x.DataPublicacao).Take(2))
                {
                    var ementa = i.Ementa.Length > 110 ? i.Ementa[..110] : i.Ementa;
                    relatorio.AppendLine($"     {i.DataPublicacao:yyyy-MM-dd} | {i.TipoAto} | {i.NumeroAto} | {ementa} | {i.Url}");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                relatorio.AppendLine($"FALHA {codigo}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        var saida = Environment.GetEnvironmentVariable("FONTES_REAIS_RELATORIO") ?? Path.Combine(Path.GetTempPath(), "fontes-reais.txt");
        await File.WriteAllTextAsync(saida, relatorio.ToString(), new UTF8Encoding(false));
    }
}
