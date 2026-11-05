using System.Text.Json;
using Agente.Application.Catalogo;
using Agente.Application.Comum;
using Agente.Domain.Catalogo;
using Microsoft.Extensions.Options;

namespace Api.Agente.Inicializacao;

/// <summary>Arquivo docs/fontes/catalogo-AAAA-MM-DD.json (campos de FonteInput + pendências).</summary>
public sealed record ArquivoCatalogo(string Versao, string DocumentoOrigem, List<FonteArquivo> Fontes);

public sealed record FonteArquivo(
    string Codigo,
    string Nome,
    string LinkOficial,
    string OQueMonitorar,
    string OrgaoEmissor,
    string Esfera,
    string? Ente,
    string Grupo,
    string Natureza,
    List<string> ResponsavelInterno,
    List<string> Formatos,
    string TipoConector,
    JsonElement? ConfiguracaoConector,
    string Periodicidade,
    string? ExcecaoDominioAprovadaPor = null);

/// <summary>
/// Carga inicial do catálogo (T040–T041): se não houver nenhuma versão, importa e ativa o arquivo
/// configurado em Coleta:ArquivoCatalogoInicial com o aprovador de Coleta:AprovadorCatalogoInicial.
/// </summary>
public static class CargaCatalogoInicial
{
    public static async Task ExecutarAsync(IServiceProvider servicos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        using var escopo = servicos.CreateScope();
        var sp = escopo.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CargaCatalogoInicial));
        var opcoes = sp.GetRequiredService<IOptions<OpcoesColeta>>().Value;

        if (await sp.GetRequiredService<IRepositorioVersoesCatalogo>().ExisteAlgumaAsync(ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(opcoes.ArquivoCatalogoInicial) || string.IsNullOrWhiteSpace(opcoes.AprovadorCatalogoInicial))
        {
            logger.LogWarning("Sem catálogo e sem carga inicial configurada (Coleta:ArquivoCatalogoInicial/AprovadorCatalogoInicial).");
            return;
        }

        var caminho = Path.IsPathRooted(opcoes.ArquivoCatalogoInicial)
            ? opcoes.ArquivoCatalogoInicial
            : Path.Combine(AppContext.BaseDirectory, opcoes.ArquivoCatalogoInicial);
        var entrada = Converter(Ler(caminho));

        var importado = await sp.GetRequiredService<ImportarCatalogo>().ExecutarAsync(entrada, opcoes.AprovadorCatalogoInicial, ct);
        if (!importado.Sucesso)
        {
            throw new InvalidOperationException("Catálogo inicial inválido: " + string.Join("; ", importado.Erros));
        }

        var ativado = await sp.GetRequiredService<AtivarVersaoCatalogo>().ExecutarAsync(entrada.Versao, ct);
        if (!ativado.Sucesso)
        {
            throw new InvalidOperationException("Falha ao ativar o catálogo inicial: " + string.Join("; ", ativado.Erros));
        }

        logger.LogInformation("Catálogo {Versao} importado e ativado com {Fontes} fonte(s).", entrada.Versao, entrada.Fontes.Count);
    }

    public static ArquivoCatalogo Ler(string caminho) =>
        JsonSerializer.Deserialize<ArquivoCatalogo>(File.ReadAllText(caminho), JsonContrato.Opcoes)
            ?? throw new InvalidOperationException($"Catálogo vazio: {caminho}");

    public static CatalogoEntrada Converter(ArquivoCatalogo a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new CatalogoEntrada(
            a.Versao,
            a.DocumentoOrigem,
            a.Fontes.Select(f => new FonteEntrada(
                f.Codigo, f.Nome, f.LinkOficial, f.OQueMonitorar, f.OrgaoEmissor, f.Esfera, f.Ente, f.Grupo, f.Natureza,
                f.ResponsavelInterno, f.Formatos, f.TipoConector,
                f.ConfiguracaoConector is { ValueKind: JsonValueKind.Object } c ? c.GetRawText() : null,
                f.Periodicidade,
                f.ExcecaoDominioAprovadaPor)).ToList());
    }
}
