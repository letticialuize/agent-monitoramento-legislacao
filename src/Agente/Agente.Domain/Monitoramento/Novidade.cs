using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Monitoramento;

/// <summary>Dados de um ato identificado na coleta.</summary>
public sealed record DadosNovidade(
    DateOnly DataPublicacao,
    string OrgaoEmissor,
    string TipoAto,
    string? NumeroAto,
    string Ementa,
    string? Trecho);

/// <summary>Resultado da classificação determinística aplicada a uma novidade.</summary>
public sealed record ClassificacaoDeterministica(
    Esfera Esfera,
    Ente? Ente,
    Grupo Grupo,
    IReadOnlyList<Perfil> Perfis,
    bool PerfilDecidido,
    IReadOnlyList<PapelResponsavel> ResponsavelInterno);

/// <summary>
/// Agregado: ato ou decisão identificado. Contém as ocorrências (onde foi encontrado) e as
/// revisões humanas. Garante as invariantes do data-model.md.
/// </summary>
public sealed class Novidade : RaizAgregado
{
    private readonly List<OcorrenciaNovidade> _ocorrencias = [];
    private readonly List<RevisaoNovidade> _revisoes = [];

    public ChaveDeduplicacao ChaveDeduplicacao { get; private set; } = null!;

    public DateOnly DataPublicacao { get; private set; }

    public string OrgaoEmissor { get; private set; } = string.Empty;

    public string TipoAto { get; private set; } = string.Empty;

    public string? NumeroAto { get; private set; }

    public string Ementa { get; private set; } = string.Empty;

    public string? Trecho { get; private set; }

    public string? LinkOficial { get; private set; }

    public Esfera Esfera { get; private set; }

    public Ente? Ente { get; private set; }

    public Grupo Grupo { get; private set; }

    public string? Tema { get; private set; }

    public Impacto? Impacto { get; private set; }

    public IReadOnlyList<Perfil> Perfis { get; private set; } = [];

    public Origem OrigemPerfil { get; private set; }

    public Origem? OrigemImpacto { get; private set; }

    /// <summary>Indício da IA; nulo enquanto não classificada. Só false oculta a novidade das consultas.</summary>
    public bool? RelacionadaTributos { get; private set; }

    /// <summary>Verdadeiro quando as regras determinísticas decidiram o perfil.</summary>
    public bool PerfilDecidido { get; private set; }

    /// <summary>Verdadeiro até a sugestão da IA (tema, impacto e, se preciso, perfil) ser aplicada ou desistida.</summary>
    public bool AguardandoClassificacaoIa { get; private set; }

    public SituacaoNovidade Situacao { get; private set; }

    public Guid? RetificaNovidadeId { get; private set; }

    public IReadOnlyList<PapelResponsavel> ResponsavelInterno { get; private set; } = [];

    public string HashConteudo { get; private set; } = string.Empty;

    public DateTimeOffset ColetadaEm { get; private set; }

    public IReadOnlyList<OcorrenciaNovidade> Ocorrencias => _ocorrencias;

    public IReadOnlyList<RevisaoNovidade> Revisoes => _revisoes;

    private Novidade() { }

    public static Resultado<Novidade> Criar(DadosNovidade dados, ChaveDeduplicacao chave, string hashConteudo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(chave);

        var erros = new List<ErroDominio>();
        if (string.IsNullOrWhiteSpace(dados.OrgaoEmissor))
        {
            erros.Add(new("novidade.orgao_obrigatorio", "Órgão emissor é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(dados.TipoAto))
        {
            erros.Add(new("novidade.tipo_obrigatorio", "Tipo do ato é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(dados.Ementa))
        {
            erros.Add(new("novidade.ementa_obrigatoria", "Ementa é obrigatória."));
        }

        if (erros.Count > 0)
        {
            return Resultado<Novidade>.Falha(erros);
        }

        return Resultado<Novidade>.Ok(new Novidade
        {
            Id = Guid.NewGuid(),
            ChaveDeduplicacao = chave,
            DataPublicacao = dados.DataPublicacao,
            OrgaoEmissor = dados.OrgaoEmissor.Trim(),
            TipoAto = dados.TipoAto.Trim(),
            NumeroAto = string.IsNullOrWhiteSpace(dados.NumeroAto) ? null : dados.NumeroAto.Trim(),
            Ementa = dados.Ementa.Trim(),
            Trecho = dados.Trecho,
            HashConteudo = hashConteudo,
            Situacao = SituacaoNovidade.Coletada,
            ColetadaEm = agora,
            CriadoEm = agora,
            AtualizadoEm = agora,
        });
    }

    /// <summary>
    /// Registra onde o ato foi encontrado. A ocorrência principal é a primeira de fonte oficial do
    /// catálogo; sem nenhuma oficial, a primeira registrada. Exatamente uma é principal.
    /// </summary>
    public void RegistrarOcorrencia(Guid? fonteId, NaturezaFonte? natureza, string url, Guid execucaoColetaId, DateTimeOffset agora)
    {
        if (_ocorrencias.Any(o => o.FonteId == fonteId && string.Equals(o.Url, url, StringComparison.Ordinal)))
        {
            return;
        }

        var nova = new OcorrenciaNovidade(Guid.NewGuid(), Id, fonteId, natureza, url, execucaoColetaId, agora);
        var principalAtual = _ocorrencias.FirstOrDefault(o => o.Principal);

        if (principalAtual is null)
        {
            nova.MarcarPrincipal(true);
        }
        else if (natureza == NaturezaFonte.Oficial && principalAtual.Natureza != NaturezaFonte.Oficial)
        {
            principalAtual.MarcarPrincipal(false);
            nova.MarcarPrincipal(true);
        }

        _ocorrencias.Add(nova);

        if (nova.Principal && natureza == NaturezaFonte.Oficial && Situacao == SituacaoNovidade.Confirmada)
        {
            LinkOficial = url;
        }

        Tocar(agora);
    }

    /// <summary>Define a situação inicial a partir das ocorrências (só uma vez, a partir de "coletada").</summary>
    public Resultado DefinirSituacaoInicial(SituacaoNovidade situacao, DateTimeOffset agora)
    {
        if (Situacao != SituacaoNovidade.Coletada)
        {
            return Resultado.Ok();
        }

        if (situacao is not (SituacaoNovidade.Confirmada or SituacaoNovidade.PendenteConfirmacao or SituacaoNovidade.FonteNaoCatalogada))
        {
            return Resultado.Falha("novidade.situacao_inicial_invalida", $"Situação inicial inválida: {situacao}.");
        }

        if (situacao == SituacaoNovidade.Confirmada)
        {
            var principal = _ocorrencias.FirstOrDefault(o => o.Principal);
            if (principal?.Natureza != NaturezaFonte.Oficial)
            {
                return Resultado.Falha("novidade.confirmacao_sem_fonte_oficial", "Confirmada exige ocorrência principal em fonte oficial do catálogo.");
            }

            LinkOficial = principal.Url;
        }

        Situacao = situacao;
        Tocar(agora);
        return Resultado.Ok();
    }

    /// <summary>Aplica esfera, ente, grupo, perfis e responsável vindos das regras determinísticas.</summary>
    public Resultado AplicarClassificacao(ClassificacaoDeterministica c, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Perfis.Count == 0)
        {
            return Resultado.Falha("novidade.perfil_obrigatorio", "A novidade deve ter ao menos um perfil.");
        }

        Esfera = c.Esfera;
        Ente = c.Ente;
        Grupo = c.Grupo;
        Perfis = c.Perfis.Distinct().ToList();
        OrigemPerfil = Origem.RegraDeterministica;
        PerfilDecidido = c.PerfilDecidido;
        AguardandoClassificacaoIa = true;
        ResponsavelInterno = c.ResponsavelInterno.Distinct().ToList();
        Tocar(agora);
        return Resultado.Ok();
    }

    /// <summary>
    /// Aplica a sugestão da IA (indício). Perfis só são substituídos quando as regras não decidiram;
    /// tema e impacto ficam sempre marcados como indício.
    /// </summary>
    public Resultado AplicarSugestaoIa(IReadOnlyList<Perfil> perfis, string? tema, Impacto? impacto, DateTimeOffset agora, bool? relacionadaTributos = null)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        if (!PerfilDecidido)
        {
            if (perfis.Count == 0)
            {
                return Resultado.Falha("novidade.perfil_obrigatorio", "A sugestão da IA deve ter ao menos um perfil.");
            }

            Perfis = perfis.Distinct().ToList();
            OrigemPerfil = Origem.IndicioIa;
        }

        AguardandoClassificacaoIa = false;

        RelacionadaTributos = relacionadaTributos;

        if (!string.IsNullOrWhiteSpace(tema))
        {
            Tema = tema.Trim();
        }

        if (impacto is not null)
        {
            Impacto = impacto;
            OrigemImpacto = Origem.IndicioIa;
        }

        Tocar(agora);
        return Resultado.Ok();
    }

    /// <summary>Encerra a espera pela IA quando a sugestão falhou (mantém a classificação das regras).</summary>
    public void EncerrarEsperaIa(DateTimeOffset agora)
    {
        AguardandoClassificacaoIa = false;
        Tocar(agora);
    }

    public void VincularRetificacao(Guid novidadeOriginalId, DateTimeOffset agora)
    {
        if (novidadeOriginalId == Id)
        {
            return;
        }

        RetificaNovidadeId = novidadeOriginalId;
        Tocar(agora);
    }

    /// <summary>Revisão humana: confirma com link de fonte oficial (FR-027, FR-028).</summary>
    public Resultado Confirmar(LinkOficial link, string revisorId, string motivo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(link);
        var podeRevisar = ValidarRevisao(revisorId, motivo);
        if (!podeRevisar.Sucesso)
        {
            return podeRevisar;
        }

        if (!link.EhDominioOficial())
        {
            return Resultado.Falha("revisao.link_nao_oficial", "A confirmação exige link de publicação oficial.");
        }

        LinkOficial = link.Url;
        Situacao = SituacaoNovidade.Confirmada;
        _revisoes.Add(new RevisaoNovidade(Guid.NewGuid(), Id, revisorId, DecisaoRevisao.Confirmar, motivo, link.Url, agora));
        Tocar(agora);
        return Resultado.Ok();
    }

    public Resultado Descartar(string revisorId, string motivo, DateTimeOffset agora)
    {
        var podeRevisar = ValidarRevisao(revisorId, motivo);
        if (!podeRevisar.Sucesso)
        {
            return podeRevisar;
        }

        Situacao = SituacaoNovidade.Descartada;
        _revisoes.Add(new RevisaoNovidade(Guid.NewGuid(), Id, revisorId, DecisaoRevisao.Descartar, motivo, null, agora));
        Tocar(agora);
        return Resultado.Ok();
    }

    public Resultado ColocarEmAnalise(string revisorId, string motivo, DateTimeOffset agora)
    {
        if (Situacao == SituacaoNovidade.EmAnalise)
        {
            return Resultado.Falha("revisao.ja_em_analise", "A novidade já está em análise.");
        }

        var podeRevisar = ValidarRevisao(revisorId, motivo);
        if (!podeRevisar.Sucesso)
        {
            return podeRevisar;
        }

        Situacao = SituacaoNovidade.EmAnalise;
        _revisoes.Add(new RevisaoNovidade(Guid.NewGuid(), Id, revisorId, DecisaoRevisao.EmAnalise, motivo, null, agora));
        Tocar(agora);
        return Resultado.Ok();
    }

    /// <summary>Indica revisão humana conforme o Princípio XVI.</summary>
    public bool RequerRevisaoHumana =>
        Situacao is SituacaoNovidade.PendenteConfirmacao or SituacaoNovidade.FonteNaoCatalogada or SituacaoNovidade.EmAnalise
        || OrigemPerfil == Origem.IndicioIa;

    public bool EstaPendenteDeRevisao =>
        Situacao is SituacaoNovidade.PendenteConfirmacao or SituacaoNovidade.FonteNaoCatalogada or SituacaoNovidade.EmAnalise;

    private Resultado ValidarRevisao(string revisorId, string motivo)
    {
        if (!EstaPendenteDeRevisao)
        {
            return Resultado.Falha("revisao.estado_invalido", $"Novidade em situação '{Situacao}' não é revisável.");
        }

        if (string.IsNullOrWhiteSpace(revisorId))
        {
            return Resultado.Falha("revisao.revisor_obrigatorio", "Revisor é obrigatório.");
        }

        return string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 3
            ? Resultado.Falha("revisao.motivo_obrigatorio", "Motivo é obrigatório (mínimo 3 caracteres).")
            : Resultado.Ok();
    }
}

/// <summary>Onde a novidade foi encontrada (entidade interna de <see cref="Novidade"/>).</summary>
public sealed class OcorrenciaNovidade : Entidade
{
    public Guid NovidadeId { get; private set; }

    /// <summary>Nulo quando a fonte não está no catálogo.</summary>
    public Guid? FonteId { get; private set; }

    /// <summary>Nulo quando a fonte não está no catálogo.</summary>
    public NaturezaFonte? Natureza { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public Guid ExecucaoColetaId { get; private set; }

    public DateTimeOffset ColetadaEm { get; private set; }

    public bool Principal { get; private set; }

    private OcorrenciaNovidade() { }

    internal OcorrenciaNovidade(Guid id, Guid novidadeId, Guid? fonteId, NaturezaFonte? natureza, string url, Guid execucaoColetaId, DateTimeOffset em)
        : base(id)
    {
        NovidadeId = novidadeId;
        FonteId = fonteId;
        Natureza = natureza;
        Url = url;
        ExecucaoColetaId = execucaoColetaId;
        ColetadaEm = em;
    }

    internal void MarcarPrincipal(bool principal) => Principal = principal;
}

public enum DecisaoRevisao { Confirmar, Descartar, EmAnalise }

/// <summary>Decisão humana sobre uma novidade pendente (entidade interna de <see cref="Novidade"/>).</summary>
public sealed class RevisaoNovidade : Entidade
{
    public Guid NovidadeId { get; private set; }

    public string RevisorId { get; private set; } = string.Empty;

    public DecisaoRevisao Decisao { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public string? LinkOficialInformado { get; private set; }

    public DateTimeOffset DecididaEm { get; private set; }

    private RevisaoNovidade() { }

    internal RevisaoNovidade(Guid id, Guid novidadeId, string revisorId, DecisaoRevisao decisao, string motivo, string? link, DateTimeOffset em)
        : base(id)
    {
        NovidadeId = novidadeId;
        RevisorId = revisorId;
        Decisao = decisao;
        Motivo = motivo.Trim();
        LinkOficialInformado = link;
        DecididaEm = em;
    }
}
