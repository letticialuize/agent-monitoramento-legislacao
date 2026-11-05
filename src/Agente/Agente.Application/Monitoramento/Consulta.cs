using System.Globalization;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Options;
using ConsultaAgregado = Agente.Domain.Operacao.Consulta;

namespace Agente.Application.Monitoramento;

public sealed record FiltroNovidades(
    IReadOnlyList<string> Termos,
    DateOnly Inicio,
    DateOnly Fim,
    IReadOnlyList<Esfera> Esferas,
    IReadOnlyList<Perfil> Perfis,
    IReadOnlyList<Grupo> Grupos,
    bool IncluirPendentes,
    bool IncluirNaoTributarias,
    int Limite);

public sealed record PaginaNovidades(IReadOnlyList<NovidadeDto> Itens, int Total, int OcultadasNaoTributarias = 0);

/// <summary>Leitura de novidades (busca textual) sem passar pelo agregado.</summary>
public interface IConsultaNovidades
{
    Task<PaginaNovidades> BuscarAsync(FiltroNovidades filtro, CancellationToken ct);

    /// <summary>Detalhe de uma novidade; descartadas não são retornadas.</summary>
    Task<NovidadeDto?> ObterAsync(Guid id, CancellationToken ct);
}

public sealed record SituacaoFontes(IReadOnlyList<string> Consultadas, IReadOnlyList<FonteComFalha> ComFalha, IReadOnlyList<string> NuncaColetadas);

public sealed record FonteComFalha(string Codigo, string Motivo);

/// <summary>Quais fontes do catálogo vigente cobrem o filtro e quais falharam no período.</summary>
public interface IConsultaSituacaoFontes
{
    Task<SituacaoFontes> ObterAsync(DateOnly inicio, DateOnly fim, IReadOnlyList<Esfera> esferas, IReadOnlyList<Grupo> grupos, CancellationToken ct);
}

public sealed record PedidoConsulta(
    string Assunto,
    DateOnly? Inicio = null,
    DateOnly? Fim = null,
    IReadOnlyList<Esfera>? Esferas = null,
    IReadOnlyList<Perfil>? Perfis = null,
    IReadOnlyList<Grupo>? Grupos = null,
    bool IncluirPendentes = true,
    string? ClienteNome = null,
    bool IncluirNaoTributarias = false);

/// <summary>
/// Consulta sob demanda por assunto (US1, FR-010..013) sobre a base coletada pelo agendador.
/// Não usa o LLM: busca, classificação e evidências são determinísticas.
/// </summary>
public sealed class ConsultarNovidades(
    IConsultaNovidades leitura,
    IConsultaSituacaoFontes situacaoFontes,
    IConsultaConsumo consumo,
    IRepositorioConsultas consultas,
    IUnidadeDeTrabalho unidade,
    IProvedorRegras regras,
    RegistradorEtapas etapas,
    IRelogio relogio,
    IOptions<OpcoesConsulta> opcoes)
{
    /// <summary>Consulta sem assunto: lista todas as novidades do período.</summary>
    public const string SemAssunto = "(todas as novidades)";

    public const string MensagemSemNovidades =
        "Sem novidades encontradas nas fontes consultadas no período. Isso não significa que não houve mudança normativa: veja as fontes consultadas e as limitações.";

    public async Task<Resultado<EnvelopeResposta>> ExecutarAsync(
        PedidoConsulta pedido,
        ContextoUsuario usuario,
        string correlacaoId,
        string? conversaId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(usuario);

        var validacao = Validar(pedido, out var periodo);
        if (!validacao.Sucesso)
        {
            return Resultado<EnvelopeResposta>.Falha(validacao.Erros);
        }

        var iniciado = relogio.AgoraUtc;
        var esferas = pedido.Esferas ?? [];
        // Sem perfil informado, a consulta é do consultor tributário (FR-008a).
        IReadOnlyList<Perfil> perfis = pedido.Perfis is { Count: > 0 } informados ? informados : [Perfil.ConsultorTributario];
        var grupos = pedido.Grupos ?? [];
        var termo = pedido.Assunto?.Trim() ?? string.Empty;
        var assunto = termo.Length == 0 ? SemAssunto : termo;

        var consulta = ConsultaAgregado.Iniciar(correlacaoId, usuario.UsuarioId, conversaId, assunto, periodo, esferas, perfis, grupos, EnvelopeResposta.VersaoAtual, iniciado);
        consultas.Adicionar(consulta);

        var limitacoes = new List<string>();
        var achados = new List<Achado>();

        // 1. Coleta: a consulta lê a base alimentada pelo agendador; aqui se apuram as fontes e falhas.
        var etapaColeta = etapas.Iniciar(consulta, EtapaFluxo.Coleta);
        var fontes = await situacaoFontes.ObterAsync(periodo.Inicio, periodo.Fim, esferas, grupos, ct);
        foreach (var f in fontes.ComFalha)
        {
            achados.Add(new Achado(CategoriaAchado.FalhaTecnica, $"Fonte indisponível ou com falha na coleta: {f.Motivo}", f.Codigo));
            limitacoes.Add($"A fonte {f.Codigo} falhou na coleta do período; novidades dela podem estar ausentes.");
        }

        if (fontes.NuncaColetadas.Count > 0)
        {
            limitacoes.Add($"Fontes ainda sem coleta concluída: {string.Join(", ", fontes.NuncaColetadas)}.");
        }

        etapas.Concluir(etapaColeta, fontes.ComFalha.Count > 0 ? StatusEtapa.Falha : StatusEtapa.Sucesso, $"{fontes.Consultadas.Count} fonte(s) consultada(s)");

        // 2. Classificação: aplicada na coleta (regras determinísticas + indício da IA).
        etapas.Concluir(etapas.Iniciar(consulta, EtapaFluxo.Classificacao), StatusEtapa.Sucesso, "Classificação aplicada na coleta.");
        etapas.NaoExecutada(consulta, EtapaFluxo.CruzamentoCliente, "Consulta sem cliente (Entrega 1).");

        // 3. Busca determinística.
        var etapaBusca = etapas.Iniciar(consulta, EtapaFluxo.Validacoes);
        var limite = Math.Max(1, opcoes.Value.LimiteItens);
        var pagina = await leitura.BuscarAsync(
            new FiltroNovidades(termo.Length == 0 ? [] : regras.Sinonimos.Expandir(termo), periodo.Inicio, periodo.Fim, esferas, perfis, grupos, pedido.IncluirPendentes, pedido.IncluirNaoTributarias, limite),
            ct);
        etapas.Concluir(etapaBusca, StatusEtapa.Sucesso, $"{pagina.Total} novidade(s) encontrada(s).");

        etapas.NaoExecutada(consulta, EtapaFluxo.Ferramentas, "Consulta estruturada não usa ferramentas externas.");
        etapas.NaoExecutada(consulta, EtapaFluxo.Llm, "Consulta estruturada não usa o LLM.");
        etapas.NaoExecutada(consulta, EtapaFluxo.ValidacaoSaida, "Sem saída de LLM a validar.");

        var omitidos = Math.Max(0, pagina.Total - pagina.Itens.Count);
        if (omitidos > 0)
        {
            limitacoes.Add($"{omitidos} novidade(s) omitida(s) pelo limite de {limite} itens; refine o assunto ou o período.");
        }

        if (pagina.OcultadasNaoTributarias > 0)
        {
            limitacoes.Add($"{pagina.OcultadasNaoTributarias} novidade(s) ocultada(s) por não ter(em) relação tributária, segundo indício da IA; use incluirNaoTributarias para conferir.");
        }

        foreach (var n in pagina.Itens.Where(n => n.OrigemPerfil == Origem.IndicioIa))
        {
            achados.Add(new Achado(CategoriaAchado.IndicioIa, "Perfil da novidade sugerido pela IA; requer revisão humana.", n.Id.ToString()));
        }

        foreach (var n in pagina.Itens.Where(n => n.Situacao is SituacaoNovidade.PendenteConfirmacao or SituacaoNovidade.FonteNaoCatalogada))
        {
            achados.Add(new Achado(
                CategoriaAchado.AusenciaEvidencia,
                n.Situacao == SituacaoNovidade.PendenteConfirmacao
                    ? "Novidade encontrada apenas em agregador; publicação oficial ainda não localizada."
                    : "Novidade de fonte fora do catálogo de fontes confiáveis; requer revisão humana.",
                n.Id.ToString()));
        }

        string? mensagemVazia = null;
        if (pagina.Itens.Count == 0)
        {
            mensagemVazia = MensagemSemNovidades;
            achados.Add(new Achado(CategoriaAchado.AusenciaEvidencia, "Nenhuma novidade encontrada nas fontes consultadas no período.", null));
        }

        var resultado = new ResultadoConsulta(
            assunto,
            new PeriodoDto(periodo.Inicio, periodo.Fim),
            null,
            fontes.Consultadas,
            fontes.ComFalha.Select(f => f.Codigo).ToList(),
            pagina.Itens,
            omitidos,
            mensagemVazia,
            AgrupadorNovidades.Agrupar(pagina.Itens, perfis),
            pagina.OcultadasNaoTributarias);

        var requerRevisao = pagina.Itens.Any(n => n.RequerRevisaoHumana);
        var status = limitacoes.Count > 0 || fontes.ComFalha.Count > 0
            ? StatusProcessamento.ConcluidoComLimitacoes
            : StatusProcessamento.Concluido;

        etapas.Concluir(etapas.Iniciar(consulta, EtapaFluxo.Resposta), StatusEtapa.Sucesso);
        var finalizado = relogio.AgoraUtc;
        consulta.Finalizar(status, requerRevisao, limitacoes, finalizado);
        await unidade.SalvarAsync(ct);

        return Resultado<EnvelopeResposta>.Ok(new EnvelopeResposta(
            EnvelopeResposta.VersaoAtual,
            consulta.Id,
            correlacaoId,
            status,
            iniciado,
            finalizado,
            requerRevisao,
            resultado,
            achados,
            limitacoes,
            await consumo.ResumirAsync(consulta.Id, ct)));
    }

    private Resultado Validar(PedidoConsulta pedido, out PeriodoConsulta periodo)
    {
        var erros = new List<ErroDominio>();
        var assunto = pedido.Assunto?.Trim() ?? string.Empty;
        if (assunto.Length is 1 or > 200)
        {
            erros.Add(new("consulta.assunto_invalido", "O assunto, quando informado, deve ter entre 2 e 200 caracteres."));
        }

        if (!string.IsNullOrWhiteSpace(pedido.ClienteNome))
        {
            erros.Add(new("consulta.cliente_indisponivel", "Consulta por cliente ainda não está disponível (Entrega 2)."));
        }

        var hoje = relogio.HojeLocal;
        var padrao = PeriodoConsulta.UltimosDias(hoje, opcoes.Value.PeriodoPadraoDias);
        var p = PeriodoConsulta.Criar(pedido.Inicio ?? (pedido.Fim?.AddDays(-(opcoes.Value.PeriodoPadraoDias - 1)) ?? padrao.Inicio), pedido.Fim ?? hoje);
        periodo = p.Sucesso ? p.Valor : padrao;
        erros.AddRange(p.Erros);

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falha(erros);
    }

    /// <summary>Converte listas de texto do contrato em enums; valores inválidos viram erro.</summary>
    public static Resultado<IReadOnlyList<T>> LerLista<T>(IEnumerable<string>? valores, string campo) where T : struct, Enum
    {
        var lista = new List<T>();
        var erros = new List<ErroDominio>();
        foreach (var v in valores ?? [])
        {
            if (Enumeracao.TentarLer<T>(v, out var e))
            {
                lista.Add(e);
            }
            else
            {
                erros.Add(new($"consulta.{campo}_invalido", string.Create(CultureInfo.InvariantCulture, $"Valor inválido em '{campo}': '{v}'.")));
            }
        }

        return erros.Count > 0 ? Resultado<IReadOnlyList<T>>.Falha(erros) : Resultado<IReadOnlyList<T>>.Ok(lista.Distinct().ToList());
    }
}
