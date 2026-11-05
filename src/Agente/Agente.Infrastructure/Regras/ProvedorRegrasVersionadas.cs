using System.Text.Json;
using Agente.Application.Comum;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Microsoft.Extensions.Options;

namespace Agente.Infrastructure.Regras;

public sealed class OpcoesRegras
{
    /// <summary>Diretório dos arquivos docs/regras/*.json (relativo à pasta da aplicação).</summary>
    public string Diretorio { get; set; } = "Regras";
}

/// <summary>Lê as regras versionadas (perfis, esfera por órgão, sinônimos) e as entrega ao domínio.</summary>
internal sealed class ProvedorRegrasVersionadas : IProvedorRegras
{
    public ProvedorRegrasVersionadas(IOptions<OpcoesRegras> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        var dir = Path.IsPathRooted(opcoes.Value.Diretorio)
            ? opcoes.Value.Diretorio
            : Path.Combine(AppContext.BaseDirectory, opcoes.Value.Diretorio);

        var perfis = Ler<ArquivoPerfis>(dir, "perfis-v1.json");
        Perfis = new RegrasPerfil(
            perfis.Versao,
            perfis.FontesDesenvolvedor,
            perfis.TermosDesenvolvedor,
            perfis.GruposConsultor.Select(g => Enum(g, $"gruposConsultor '{g}'", out Grupo v) ? v : default).ToList(),
            perfis.TermosConsultor);

        var esfera = Ler<ArquivoEsfera>(dir, "esfera-por-orgao-v1.json");
        EsferaPorOrgao = new RegrasEsferaPorOrgao(
            esfera.Versao,
            Enum(esfera.EsferaPadraoNacional, "esferaPadraoNacional", out Esfera padrao) ? padrao : default,
            esfera.Regras.Select(r => new RegraEsferaPorOrgao(r.Padrao, Enum(r.Esfera, $"regra '{r.Padrao}'", out Esfera e) ? e : default, r.Uf)).ToList());

        var sinonimos = Ler<ArquivoSinonimos>(dir, "sinonimos-busca-v1.json");
        Sinonimos = new DicionarioSinonimos(sinonimos.Versao, sinonimos.Grupos);
    }

    public RegrasPerfil Perfis { get; }

    public RegrasEsferaPorOrgao EsferaPorOrgao { get; }

    public DicionarioSinonimos Sinonimos { get; }

    private static T Ler<T>(string dir, string arquivo)
    {
        var caminho = Path.Combine(dir, arquivo);
        if (!File.Exists(caminho))
        {
            throw new InvalidOperationException($"Arquivo de regras ausente: {caminho}");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho), JsonContrato.Opcoes)
            ?? throw new InvalidOperationException($"Arquivo de regras inválido: {caminho}");
    }

    private static bool Enum<T>(string valor, string onde, out T resultado) where T : struct, System.Enum =>
        Enumeracao.TentarLer(valor, out resultado)
            ? true
            : throw new InvalidOperationException($"Valor inválido em regras ({onde}): '{valor}'.");

    private sealed record ArquivoPerfis(string Versao, List<string> FontesDesenvolvedor, List<string> TermosDesenvolvedor, List<string> GruposConsultor, List<string> TermosConsultor);

    private sealed record ArquivoEsfera(string Versao, string EsferaPadraoNacional, List<RegraArquivo> Regras);

    private sealed record RegraArquivo(string Padrao, string Esfera, string? Uf);

    private sealed record ArquivoSinonimos(string Versao, List<List<string>> Grupos);
}
