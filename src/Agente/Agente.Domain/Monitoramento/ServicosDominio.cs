using System.Security.Cryptography;
using System.Text;
using Agente.Domain.Catalogo;
using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Monitoramento;

/// <summary>Deduplicação de atos entre fontes (research R-05).</summary>
public sealed class DeduplicadorNovidades
{
    public ChaveDeduplicacao GerarChave(DadosNovidade dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        return ChaveDeduplicacao.Criar(dados.OrgaoEmissor, dados.TipoAto, dados.NumeroAto, dados.DataPublicacao, dados.Ementa);
    }

    public string HashConteudo(DadosNovidade dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var texto = Texto.Normalizar($"{dados.Ementa} {dados.Trecho}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }
}

/// <summary>
/// Situação inicial (FR-003): fonte oficial do catálogo → confirmada; só agregador de apoio →
/// pendente de confirmação; sem fonte do catálogo → fonte não catalogada.
/// </summary>
public sealed class ClassificadorSituacao
{
    public SituacaoNovidade Classificar(IEnumerable<NaturezaFonte?> naturezasDasOcorrencias)
    {
        ArgumentNullException.ThrowIfNull(naturezasDasOcorrencias);
        var lista = naturezasDasOcorrencias.ToList();
        if (lista.Contains(NaturezaFonte.Oficial))
        {
            return SituacaoNovidade.Confirmada;
        }

        return lista.Contains(NaturezaFonte.AgregadorApoio)
            ? SituacaoNovidade.PendenteConfirmacao
            : SituacaoNovidade.FonteNaoCatalogada;
    }
}

/// <summary>Regra versionada "órgão emissor → esfera" para fontes de abrangência nacional.</summary>
public sealed record RegraEsferaPorOrgao(string Padrao, Esfera Esfera, string? Uf);

public sealed record RegrasEsferaPorOrgao(string Versao, Esfera EsferaPadraoNacional, IReadOnlyList<RegraEsferaPorOrgao> Regras);

public sealed record ResultadoEsfera(Esfera Esfera, Ente? Ente, string? Limitacao);

/// <summary>
/// Esfera da novidade: herdada da fonte; para fonte "nacional", definida pelo órgão emissor do item
/// segundo a tabela versionada (o domínio recebe a tabela, não lê arquivos).
/// </summary>
public sealed class ResolvedorEsfera
{
    public ResultadoEsfera Resolver(Fonte fonte, string orgaoEmissorItem, RegrasEsferaPorOrgao regras)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        ArgumentNullException.ThrowIfNull(regras);

        switch (fonte.Esfera)
        {
            case EsferaFonte.Federal:
                return new ResultadoEsfera(Esfera.Federal, fonte.Ente, null);
            case EsferaFonte.Estadual:
                return new ResultadoEsfera(Esfera.Estadual, fonte.Ente, null);
            case EsferaFonte.Municipal:
                return new ResultadoEsfera(Esfera.Municipal, fonte.Ente, null);
        }

        var orgao = Texto.Normalizar(orgaoEmissorItem);
        var palavras = orgao.Split([' ', '-', '/', ',', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var regra in regras.Regras)
        {
            var casa = regra.Padrao.EndsWith('*')
                ? palavras.Any(p => p.StartsWith(Texto.Normalizar(regra.Padrao.TrimEnd('*')), StringComparison.Ordinal))
                : Texto.ContemTermo(orgao, regra.Padrao);
            if (casa)
            {
                var ente = regra.Uf is null ? null : Ente.Criar(regra.Uf) is { Sucesso: true } e ? e.Valor : null;
                return new ResultadoEsfera(regra.Esfera, ente, null);
            }
        }

        return new ResultadoEsfera(
            regras.EsferaPadraoNacional,
            null,
            $"Órgão '{orgaoEmissorItem}' sem regra de esfera ({regras.Versao}); aplicada esfera padrão {regras.EsferaPadraoNacional}.");
    }
}

/// <summary>Regras versionadas de perfil (configuração revisada pela equipe, não regra fiscal).</summary>
public sealed record RegrasPerfil(
    string Versao,
    IReadOnlyList<string> FontesDesenvolvedor,
    IReadOnlyList<string> TermosDesenvolvedor,
    IReadOnlyList<Grupo> GruposConsultor,
    IReadOnlyList<string> TermosConsultor);

public sealed record ResultadoPerfil(IReadOnlyList<Perfil> Perfis, bool Decidido);

/// <summary>
/// Perfil por regras (FR-008): layout, adequação de sistemas ou atualização de API → desenvolvedor;
/// especificações da legislação e notícias → consultor tributário; ambos quando atende aos dois.
/// Quando nenhuma regra decide, devolve os dois perfis como padrão conservador e marca
/// <see cref="ResultadoPerfil.Decidido"/> = false para refinamento pela IA.
/// </summary>
public sealed class ClassificadorPerfilDeterministico
{
    public ResultadoPerfil Classificar(string codigoFonte, Grupo grupo, string tipoAto, string ementa, RegrasPerfil regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        var texto = Texto.Normalizar($"{tipoAto} {ementa}");

        var fonteDev = regras.FontesDesenvolvedor.Any(f => string.Equals(f, codigoFonte, StringComparison.OrdinalIgnoreCase));
        var termoDev = regras.TermosDesenvolvedor.Any(t => Texto.ContemTermo(texto, t));
        var termoConsultor = regras.TermosConsultor.Any(t => Texto.ContemTermo(texto, t));
        var grupoConsultor = regras.GruposConsultor.Contains(grupo) && !fonteDev;

        var perfis = new List<Perfil>();
        if (grupoConsultor || termoConsultor)
        {
            perfis.Add(Perfil.ConsultorTributario);
        }

        if (fonteDev || termoDev)
        {
            perfis.Add(Perfil.Desenvolvedor);
        }

        return perfis.Count > 0
            ? new ResultadoPerfil(perfis, true)
            : new ResultadoPerfil([Perfil.ConsultorTributario, Perfil.Desenvolvedor], false);
    }
}
