using Agente.Application.Comum;
using Agente.Application.Portas;
using Agente.Domain.Catalogo;
using Agente.Domain.Comum;

namespace Agente.Application.Catalogo;

/// <summary>Fonte como descrita no arquivo/requisição de catálogo (FonteInput do OpenAPI).</summary>
public sealed record FonteEntrada(
    string Codigo,
    string Nome,
    string LinkOficial,
    string OQueMonitorar,
    string OrgaoEmissor,
    string Esfera,
    string? Ente,
    string Grupo,
    string Natureza,
    IReadOnlyList<string> ResponsavelInterno,
    IReadOnlyList<string> Formatos,
    string TipoConector,
    string? ConfiguracaoConector,
    string Periodicidade,
    string? ExcecaoDominioAprovadaPor = null);

public sealed record CatalogoEntrada(string Versao, string DocumentoOrigem, IReadOnlyList<FonteEntrada> Fontes);

/// <summary>
/// Cria uma nova versão do catálogo. Se qualquer fonte for inválida, a versão inteira é recusada
/// (FR-029, FR-030). A ativação é feita por <see cref="AtivarVersaoCatalogo"/>.
/// </summary>
public sealed class ImportarCatalogo(
    IRepositorioVersoesCatalogo repositorio,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio)
{
    private readonly ValidadorConfiabilidadeFonte _validador = new();

    public async Task<Resultado<Guid>> ExecutarAsync(CatalogoEntrada entrada, string aprovadoPor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var agora = relogio.AgoraUtc;

        if (await repositorio.ObterPorVersaoAsync(entrada.Versao, ct) is not null)
        {
            return Resultado<Guid>.Falha("catalogo.versao_existente", $"A versão '{entrada.Versao}' já existe.");
        }

        var criada = VersaoCatalogo.Criar(entrada.Versao, entrada.DocumentoOrigem, aprovadoPor, agora);
        if (!criada.Sucesso)
        {
            return Resultado<Guid>.Falha(criada.Erros);
        }

        var versao = criada.Valor;
        var erros = new List<ErroDominio>();
        foreach (var f in entrada.Fontes)
        {
            var dados = Converter(f, erros);
            if (dados is null)
            {
                continue;
            }

            erros.AddRange(versao.AdicionarFonte(dados, _validador, agora).Erros);
        }

        if (entrada.Fontes.Count == 0)
        {
            erros.Add(new("catalogo.sem_fontes", "O catálogo deve ter ao menos uma fonte."));
        }

        if (erros.Count > 0)
        {
            return Resultado<Guid>.Falha(erros);
        }

        repositorio.Adicionar(versao);
        await unidade.SalvarAsync(ct);
        return Resultado<Guid>.Ok(versao.Id);
    }

    private static DadosFonte? Converter(FonteEntrada f, List<ErroDominio> erros)
    {
        var errosAntes = erros.Count;
        var esfera = Enumeracao.Ler<EsferaFonte>(f.Esfera, "esfera", f.Codigo, erros);
        var grupo = Enumeracao.Ler<Grupo>(f.Grupo, "grupo", f.Codigo, erros);
        var natureza = Enumeracao.Ler<NaturezaFonte>(f.Natureza, "natureza", f.Codigo, erros);
        var conector = Enumeracao.Ler<TipoConector>(f.TipoConector, "tipoConector", f.Codigo, erros);
        var periodicidade = Enumeracao.Ler<Periodicidade>(f.Periodicidade, "periodicidade", f.Codigo, erros);
        var responsaveis = (f.ResponsavelInterno ?? []).Select(r => Enumeracao.Ler<PapelResponsavel>(r, "responsavelInterno", f.Codigo, erros)).ToList();
        var formatos = (f.Formatos ?? []).Select(r => Enumeracao.Ler<Formato>(r, "formatos", f.Codigo, erros)).ToList();

        if (erros.Count > errosAntes)
        {
            return null;
        }

        return new DadosFonte(
            f.Codigo, f.Nome, f.LinkOficial, f.OQueMonitorar, f.OrgaoEmissor, esfera, f.Ente, grupo, natureza,
            responsaveis, formatos, conector, f.ConfiguracaoConector, periodicidade, f.ExcecaoDominioAprovadaPor);
    }
}

/// <summary>Ativa uma versão do catálogo e desativa a vigente, na mesma transação.</summary>
public sealed class AtivarVersaoCatalogo(
    IRepositorioVersoesCatalogo repositorio,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio)
{
    public async Task<Resultado> ExecutarAsync(string versao, CancellationToken ct)
    {
        var alvo = await repositorio.ObterPorVersaoAsync(versao, ct);
        if (alvo is null)
        {
            return Resultado.Falha("catalogo.versao_inexistente", $"Versão '{versao}' não encontrada.");
        }

        var agora = relogio.AgoraUtc;
        var vigente = await repositorio.ObterVigenteAsync(ct);
        if (vigente is not null && vigente.Id != alvo.Id)
        {
            vigente.Desativar(agora);
        }

        alvo.Ativar(agora);
        await unidade.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
