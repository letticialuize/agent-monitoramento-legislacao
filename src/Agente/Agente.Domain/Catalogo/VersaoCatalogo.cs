using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Catalogo;

/// <summary>Dados para incluir uma fonte numa versão do catálogo.</summary>
public sealed record DadosFonte(
    string Codigo,
    string Nome,
    string LinkOficial,
    string OQueMonitorar,
    string OrgaoEmissor,
    EsferaFonte Esfera,
    string? Ente,
    Grupo Grupo,
    NaturezaFonte Natureza,
    IReadOnlyList<PapelResponsavel> ResponsavelInterno,
    IReadOnlyList<Formato> Formatos,
    TipoConector TipoConector,
    string? ConfiguracaoConector,
    Periodicidade Periodicidade,
    string? ExcecaoDominioAprovadaPor = null);

/// <summary>Agregado: versão aprovada do catálogo de fontes confiáveis, com suas fontes.</summary>
public sealed class VersaoCatalogo : RaizAgregado
{
    private readonly List<Fonte> _fontes = [];

    public string Versao { get; private set; } = string.Empty;

    public string DocumentoOrigem { get; private set; } = string.Empty;

    public string AprovadoPor { get; private set; } = string.Empty;

    public bool Vigente { get; private set; }

    public IReadOnlyList<Fonte> Fontes => _fontes;

    private VersaoCatalogo() { }

    public static Resultado<VersaoCatalogo> Criar(string versao, string documentoOrigem, string aprovadoPor, DateTimeOffset agora)
    {
        var erros = new List<ErroDominio>();
        if (string.IsNullOrWhiteSpace(versao))
        {
            erros.Add(new("catalogo.versao_obrigatoria", "Versão é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(documentoOrigem))
        {
            erros.Add(new("catalogo.documento_obrigatorio", "Documento de origem é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(aprovadoPor))
        {
            erros.Add(new("catalogo.aprovador_obrigatorio", "Aprovador é obrigatório."));
        }

        if (erros.Count > 0)
        {
            return Resultado<VersaoCatalogo>.Falha(erros);
        }

        return Resultado<VersaoCatalogo>.Ok(new VersaoCatalogo
        {
            Id = Guid.NewGuid(),
            Versao = versao.Trim(),
            DocumentoOrigem = documentoOrigem.Trim(),
            AprovadoPor = aprovadoPor.Trim(),
            CriadoEm = agora,
            AtualizadoEm = agora,
        });
    }

    public Resultado AdicionarFonte(DadosFonte dados, ValidadorConfiabilidadeFonte validador, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(validador);

        var erros = new List<ErroDominio>();
        var codigo = CodigoFonte.Criar(dados.Codigo);
        var link = ObjetosDeValor.LinkOficial.Criar(dados.LinkOficial);
        erros.AddRange(codigo.Erros);
        erros.AddRange(link.Erros);

        Obrigatorio(dados.Nome, "fonte.nome_obrigatorio", "Nome é obrigatório.", erros);
        Obrigatorio(dados.OQueMonitorar, "fonte.o_que_monitorar_obrigatorio", "'O que monitorar' é obrigatório.", erros);
        Obrigatorio(dados.OrgaoEmissor, "fonte.orgao_obrigatorio", "Órgão emissor é obrigatório.", erros);

        Ente? ente = null;
        if (dados.Esfera is EsferaFonte.Estadual or EsferaFonte.Municipal)
        {
            var e = Ente.Parse(dados.Ente);
            erros.AddRange(e.Erros);
            if (e.Sucesso)
            {
                ente = e.Valor;
                if (dados.Esfera == EsferaFonte.Municipal && ente.Municipio is null)
                {
                    erros.Add(new("fonte.municipio_obrigatorio", "Fonte municipal exige 'UF-Municipio'."));
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(dados.Ente))
        {
            var e = Ente.Parse(dados.Ente);
            erros.AddRange(e.Erros);
            ente = e.Sucesso ? e.Valor : null;
        }

        if (dados.ResponsavelInterno.Count == 0)
        {
            erros.Add(new("fonte.responsavel_obrigatorio", "Responsável interno é obrigatório."));
        }

        if (codigo.Sucesso && _fontes.Any(f => f.Codigo == codigo.Valor))
        {
            erros.Add(new("fonte.codigo_duplicado", $"Código '{codigo.Valor}' já existe nesta versão."));
        }

        if (link.Sucesso && !string.IsNullOrWhiteSpace(dados.OrgaoEmissor))
        {
            erros.AddRange(validador.Validar(dados.Natureza, link.Valor, dados.OrgaoEmissor, dados.ExcecaoDominioAprovadaPor).Erros);
        }

        if (erros.Count > 0)
        {
            return Resultado.Falha(erros.Select(e => e with { Mensagem = $"[{dados.Codigo}] {e.Mensagem}" }));
        }

        _fontes.Add(new Fonte(Guid.NewGuid(), Id, codigo.Valor, dados, link.Valor, ente));
        Tocar(agora);
        return Resultado.Ok();
    }

    /// <summary>Marca esta versão como vigente. O caso de uso desativa a anterior na mesma transação.</summary>
    public void Ativar(DateTimeOffset agora)
    {
        Vigente = true;
        Tocar(agora);
    }

    public void Desativar(DateTimeOffset agora)
    {
        Vigente = false;
        Tocar(agora);
    }

    public Fonte? ObterFonte(Guid fonteId) => _fontes.FirstOrDefault(f => f.Id == fonteId);

    public IEnumerable<Fonte> FontesAtivas() => _fontes.Where(f => f.Ativa);

    private static void Obrigatorio(string? valor, string codigo, string mensagem, List<ErroDominio> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            erros.Add(new(codigo, mensagem));
        }
    }
}

/// <summary>Fonte do catálogo (entidade interna de <see cref="VersaoCatalogo"/>).</summary>
public sealed class Fonte : Entidade
{
    public Guid VersaoCatalogoId { get; private set; }

    public CodigoFonte Codigo { get; private set; } = null!;

    public string Nome { get; private set; } = string.Empty;

    public LinkOficial LinkOficial { get; private set; } = null!;

    public string OQueMonitorar { get; private set; } = string.Empty;

    public string OrgaoEmissor { get; private set; } = string.Empty;

    public EsferaFonte Esfera { get; private set; }

    public Ente? Ente { get; private set; }

    public Grupo Grupo { get; private set; }

    public NaturezaFonte Natureza { get; private set; }

    public IReadOnlyList<PapelResponsavel> ResponsavelInterno { get; private set; } = [];

    public IReadOnlyList<Formato> Formatos { get; private set; } = [];

    public TipoConector TipoConector { get; private set; }

    public string? ConfiguracaoConector { get; private set; }

    public Periodicidade Periodicidade { get; private set; }

    public bool Ativa { get; private set; }

    public string? ExcecaoDominioAprovadaPor { get; private set; }

    private Fonte() { }

    internal Fonte(Guid id, Guid versaoId, CodigoFonte codigo, DadosFonte d, LinkOficial link, Ente? ente) : base(id)
    {
        VersaoCatalogoId = versaoId;
        Codigo = codigo;
        Nome = d.Nome.Trim();
        LinkOficial = link;
        OQueMonitorar = d.OQueMonitorar.Trim();
        OrgaoEmissor = d.OrgaoEmissor.Trim();
        Esfera = d.Esfera;
        Ente = ente;
        Grupo = d.Grupo;
        Natureza = d.Natureza;
        ResponsavelInterno = d.ResponsavelInterno.Distinct().ToList();
        Formatos = d.Formatos.Distinct().ToList();
        TipoConector = d.TipoConector;
        ConfiguracaoConector = d.ConfiguracaoConector;
        Periodicidade = d.Periodicidade;
        ExcecaoDominioAprovadaPor = d.ExcecaoDominioAprovadaPor;
        Ativa = true;
    }
}
