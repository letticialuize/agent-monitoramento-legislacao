using System.Text;
using System.Text.Json.Nodes;
using Agente.Application.Comum;
using Agente.Application.Llm;
using Agente.Application.Portas;
using Agente.Domain.Catalogo;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agente.Application.Monitoramento;

public sealed record ResultadoColetaFonte(Guid ExecucaoId, string FonteCodigo, StatusExecucao Status, int NovasNovidades, int ItensRecebidos, string? Erro);

/// <summary>
/// Coleta uma fonte do catálogo vigente: chama o conector, deduplica, define situação, esfera,
/// grupo e perfil por regras determinísticas e registra falhas como falha técnica (FR-001..009).
/// </summary>
public sealed class ColetarFonte(
    IEnumerable<IConectorFonte> conectores,
    IRepositorioVersoesCatalogo catalogo,
    IRepositorioNovidades novidades,
    IRepositorioExecucoesColeta execucoes,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio,
    IProvedorRegras regras,
    ILogger<ColetarFonte> logger)
{
    private readonly DeduplicadorNovidades _deduplicador = new();
    private readonly ClassificadorSituacao _situacao = new();
    private readonly ResolvedorEsfera _esfera = new();
    private readonly ClassificadorPerfilDeterministico _perfil = new();

    public async Task<Resultado<ResultadoColetaFonte>> ExecutarAsync(Guid fonteId, DateOnly desde, DateOnly ate, string correlacaoId, CancellationToken ct)
    {
        var versao = await catalogo.ObterVigenteAsync(ct);
        var fonte = versao?.ObterFonte(fonteId);
        if (versao is null || fonte is null)
        {
            return Resultado<ResultadoColetaFonte>.Falha("coleta.fonte_inexistente", $"Fonte {fonteId} não está no catálogo vigente.");
        }

        var execucao = ExecucaoColeta.Iniciar(fonte.Id, correlacaoId, relogio.AgoraUtc);
        execucoes.Adicionar(execucao);

        var conector = conectores.FirstOrDefault(c => c.Tipo == fonte.TipoConector);
        if (conector is null)
        {
            var motivo = $"Conector '{fonte.TipoConector}' indisponível: formato da fonte ainda não confirmado em documentação oficial (Princípio II).";
            execucao.Falhar(motivo, relogio.AgoraUtc);
            await unidade.SalvarAsync(ct);
            return Resultado<ResultadoColetaFonte>.Ok(new(execucao.Id, fonte.Codigo.Valor, execucao.Status, 0, 0, motivo));
        }

        ResultadoConector resultado;
        try
        {
            resultado = await conector.ColetarAsync(ParaColeta(fonte), desde, ate, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha técnica na coleta da fonte {Fonte} (execução {ExecucaoId})", fonte.Codigo, execucao.Id);
            execucao.Falhar(Resumir(ex), relogio.AgoraUtc);
            await unidade.SalvarAsync(ct);
            return Resultado<ResultadoColetaFonte>.Ok(new(execucao.Id, fonte.Codigo.Valor, execucao.Status, 0, 0, execucao.Erro));
        }

        var hostsCatalogo = versao.FontesAtivas().Select(f => f.LinkOficial.Host).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var novasNoLote = new Dictionary<string, Novidade>(StringComparer.Ordinal);
        var invalidos = 0;
        var agora = relogio.AgoraUtc;

        foreach (var item in resultado.Itens)
        {
            var dados = new DadosNovidade(
                item.DataPublicacao,
                string.IsNullOrWhiteSpace(item.OrgaoEmissor) ? fonte.OrgaoEmissor : item.OrgaoEmissor,
                item.TipoAto,
                item.NumeroAto,
                item.Ementa,
                item.Trecho);

            var chave = _deduplicador.GerarChave(dados);
            var (fonteOcorrencia, natureza) = NaturezaDaOcorrencia(fonte, item.Url, hostsCatalogo);

            if (novasNoLote.TryGetValue(chave.Valor, out var noLote))
            {
                noLote.RegistrarOcorrencia(fonteOcorrencia, natureza, item.Url, execucao.Id, agora);
                continue;
            }

            var existente = await novidades.ObterPorChaveAsync(chave, ct);
            if (existente is not null)
            {
                existente.RegistrarOcorrencia(fonteOcorrencia, natureza, item.Url, execucao.Id, agora);
                continue;
            }

            var criada = Novidade.Criar(dados, chave, _deduplicador.HashConteudo(dados), agora);
            if (!criada.Sucesso)
            {
                invalidos++;
                logger.LogWarning("Item inválido da fonte {Fonte}: {Erros}", fonte.Codigo, string.Join("; ", criada.Erros));
                continue;
            }

            var novidade = criada.Valor;
            novidade.RegistrarOcorrencia(fonteOcorrencia, natureza, item.Url, execucao.Id, agora);
            novidade.DefinirSituacaoInicial(_situacao.Classificar(novidade.Ocorrencias.Select(o => o.Natureza)), agora);

            var esfera = _esfera.Resolver(fonte, dados.OrgaoEmissor, regras.EsferaPorOrgao);
            if (esfera.Limitacao is not null)
            {
                logger.LogInformation("{Limitacao} (fonte {Fonte})", esfera.Limitacao, fonte.Codigo);
            }

            var perfil = _perfil.Classificar(fonte.Codigo.Valor, fonte.Grupo, dados.TipoAto, dados.Ementa, regras.Perfis);
            novidade.AplicarClassificacao(
                new ClassificacaoDeterministica(esfera.Esfera, esfera.Ente, fonte.Grupo, perfil.Perfis, perfil.Decidido, fonte.ResponsavelInterno),
                agora);

            novidades.Adicionar(novidade);
            novasNoLote[chave.Valor] = novidade;
        }

        var aviso = invalidos > 0
            ? $"{invalidos} item(ns) descartado(s) por dados obrigatórios ausentes. {resultado.Aviso}".Trim()
            : resultado.Aviso;
        execucao.Concluir(novasNoLote.Count, resultado.Parcial || invalidos > 0, aviso, relogio.AgoraUtc);
        await unidade.SalvarAsync(ct);

        return Resultado<ResultadoColetaFonte>.Ok(new(execucao.Id, fonte.Codigo.Valor, execucao.Status, novasNoLote.Count, resultado.Itens.Count, aviso));
    }

    /// <summary>
    /// Agregador de apoio → ocorrência de agregador. Fonte oficial: o link do item precisa estar num
    /// host do catálogo ou em domínio oficial (.gov.br/.leg.br/.jus.br — ex.: documento do Senado em
    /// normas.leg.br); caso contrário é tratado como fonte não catalogada.
    /// </summary>
    internal static (Guid? FonteId, NaturezaFonte? Natureza) NaturezaDaOcorrencia(Fonte fonte, string url, IReadOnlySet<string> hostsCatalogo)
    {
        if (fonte.Natureza == NaturezaFonte.AgregadorApoio)
        {
            return (fonte.Id, NaturezaFonte.AgregadorApoio);
        }

        var link = Domain.Catalogo.ObjetosDeValor.LinkOficial.Criar(url);
        if (!link.Sucesso)
        {
            return (null, null);
        }

        var host = link.Valor.Host;
        var noCatalogo = hostsCatalogo.Any(h => host == h || host.EndsWith("." + h, StringComparison.Ordinal));
        return noCatalogo || link.Valor.EhDominioOficial() ? (fonte.Id, NaturezaFonte.Oficial) : (null, null);
    }

    private static FonteParaColeta ParaColeta(Fonte f) =>
        new(f.Id, f.Codigo.Valor, f.Nome, f.LinkOficial.Url, f.OrgaoEmissor, f.Natureza, f.TipoConector, f.ConfiguracaoConector);

    private static string Resumir(Exception ex)
    {
        var msg = $"{ex.GetType().Name}: {ex.Message}";
        return msg.Length > 500 ? msg[..500] : msg;
    }
}

/// <summary>Executa a coleta das fontes devidas do catálogo vigente e a classificação pela IA.</summary>
public sealed class ExecutarCicloColeta(
    IRepositorioVersoesCatalogo catalogo,
    IRepositorioExecucoesColeta execucoes,
    ColetarFonte coletarFonte,
    ClassificarNovidadesComLlm classificar,
    IRelogio relogio,
    IOptions<OpcoesColeta> opcoes,
    ILogger<ExecutarCicloColeta> logger)
{
    private readonly AgendaColeta _agenda = new();

    public async Task<IReadOnlyList<ResultadoColetaFonte>> ExecutarAsync(string correlacaoId, CancellationToken ct)
    {
        var versao = await catalogo.ObterVigenteAsync(ct);
        if (versao is null)
        {
            logger.LogWarning("Nenhum catálogo vigente; coleta não executada.");
            return [];
        }

        var hoje = relogio.HojeLocal;
        var resultados = new List<ResultadoColetaFonte>();
        foreach (var fonte in versao.FontesAtivas().ToList())
        {
            var ultima = await execucoes.UltimaExecucaoBemSucedidaAsync(fonte.Id, ct);
            var ultimaLocal = ultima is null ? (DateOnly?)null : DateOnly.FromDateTime(relogio.ParaLocal(ultima.Value).DateTime);
            if (!_agenda.EstaDevida(fonte.Periodicidade, ultimaLocal, hoje))
            {
                continue;
            }

            var desde = ultimaLocal ?? hoje.AddDays(-Math.Max(1, opcoes.Value.JanelaInicialDias));
            var r = await coletarFonte.ExecutarAsync(fonte.Id, desde, hoje, correlacaoId, ct);
            if (r.Sucesso)
            {
                resultados.Add(r.Valor);
            }
        }

        // Esvazia a fila por lotes: para no primeiro lote sem resultado (fila vazia ou falha do LLM).
        for (var lote = 0; lote < Math.Max(1, opcoes.Value.MaxLotesClassificacaoIa); lote++)
        {
            if (await classificar.ExecutarAsync(correlacaoId, ct) == 0)
            {
                break;
            }
        }

        return resultados;
    }
}

/// <summary>
/// Sugestão da IA (indício) para tema, impacto e — quando as regras não decidiram — perfil,
/// com o contrato llm-classificacao-novidade (Princípios III, XI).
/// </summary>
public sealed class ClassificarNovidadesComLlm(
    IRepositorioNovidades novidades,
    IUnidadeDeTrabalho unidade,
    ValidadorSaidaLlm llm,
    IRelogio relogio,
    IOptions<OpcoesColeta> opcoes,
    ILogger<ClassificarNovidadesComLlm> logger)
{
    internal const string PromptSistema =
        ConteudoNaoConfiavel.RegrasBase +
        """

        Tarefa: para cada novidade normativa recebida, sugerir:
        - perfis: "desenvolvedor" quando tratar de layout, leiaute, schema, nota técnica, adequação de sistemas ou atualização de API; "consultor_tributario" quando tratar de especificações da legislação (obrigações, alíquotas, prazos, interpretação, jurisprudência) ou notícias sobre legislação; ambos quando atender aos dois.
        - relacionadaTributos: true quando tratar de tributos, legislação tributária, obrigações fiscais, documentos fiscais eletrônicos e seus sistemas, fiscalização tributária ou jurisprudência tributária; false nos demais assuntos, mesmo que venha de órgão fazendário ou tribunal, como operações de apreensão, combate a crimes e contrabando (mesmo conduzidas pela Receita Federal), finanças públicas (dívida pública, Tesouro, resultado fiscal), mercado de carbono, agendas de sessões e eventos institucionais. Na dúvida, true.
        - tema: rótulo curto (até 120 caracteres).
        - impacto: "alto", "medio" ou "baixo", considerando apenas a ementa.
        - justificativa: até 500 caracteres, baseada apenas na ementa fornecida.
        Responda somente com o JSON do schema, incluindo todas as novidades recebidas e nenhuma outra.
        """;

    public async Task<int> ExecutarAsync(string correlacaoId, CancellationToken ct)
    {
        if (!llm.Disponivel)
        {
            return 0;
        }

        var lote = await novidades.ListarAguardandoClassificacaoIaAsync(Math.Max(1, opcoes.Value.LoteClassificacaoIa), ct);
        if (lote.Count == 0)
        {
            return 0;
        }

        var ids = lote.ToDictionary(n => n.Id.ToString());
        var contexto = new ContextoExecucao(Guid.NewGuid(), correlacaoId);
        var resultado = await llm.GerarAsync(
            contexto,
            FinalidadeLlm.Classificacao,
            ContratosLlm.ClassificacaoNovidade,
            PromptSistema,
            MontarMensagem(lote),
            json => ValidarSemantica(json, ids.Keys),
            ct);

        var agora = relogio.AgoraUtc;
        var aplicadas = 0;
        if (resultado.Sucesso)
        {
            foreach (var c in resultado.Valor["classificacoes"]!.AsArray())
            {
                var novidade = ids[c!["novidadeId"]!.GetValue<string>()];
                var perfis = c["perfis"]!.AsArray()
                    .Select(p => Enumeracao.TentarLer<Perfil>(p!.GetValue<string>(), out var v) ? v : (Perfil?)null)
                    .OfType<Perfil>()
                    .ToList();
                Enumeracao.TentarLer<Impacto>(c["impacto"]!.GetValue<string>(), out var impacto);
                if (novidade.AplicarSugestaoIa(perfis, c["tema"]!.GetValue<string>(), impacto, agora, c["relacionadaTributos"]!.GetValue<bool>()).Sucesso)
                {
                    aplicadas++;
                }
            }
        }
        else
        {
            logger.LogWarning(
                "Classificação pela IA não aplicada (falha_tecnica, execução {ExecucaoId}): {Erros}",
                contexto.ExecucaoId, string.Join("; ", resultado.Erros));
        }

        foreach (var n in lote.Where(n => n.AguardandoClassificacaoIa))
        {
            n.EncerrarEsperaIa(agora);
        }

        await unidade.SalvarAsync(ct);
        return aplicadas;
    }

    private static string MontarMensagem(IReadOnlyList<Novidade> lote)
    {
        var sb = new StringBuilder("Novidades para classificar:\n");
        foreach (var n in lote)
        {
            sb.Append("- novidadeId: ").AppendLine(n.Id.ToString())
              .Append("  orgao: ").AppendLine(n.OrgaoEmissor)
              .Append("  tipo: ").Append(n.TipoAto).Append(' ').AppendLine(n.NumeroAto ?? string.Empty)
              .Append("  dataPublicacao: ").AppendLine(n.DataPublicacao.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
              .AppendLine(ConteudoNaoConfiavel.Delimitar("ementa", n.Ementa));
        }

        return sb.ToString();
    }

    internal static IReadOnlyList<string> ValidarSemantica(JsonNode json, IEnumerable<string> idsEntrada)
    {
        var esperados = idsEntrada.ToHashSet(StringComparer.Ordinal);
        var erros = new List<string>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in json["classificacoes"]?.AsArray() ?? [])
        {
            var id = c?["novidadeId"]?.GetValue<string>() ?? string.Empty;
            if (!esperados.Contains(id))
            {
                erros.Add($"novidadeId '{id}' não existe na entrada.");
            }
            else if (!vistos.Add(id))
            {
                erros.Add($"novidadeId '{id}' repetido.");
            }

            if ((c?["perfis"]?.AsArray().Count ?? 0) == 0)
            {
                erros.Add($"novidadeId '{id}' sem perfis.");
            }

            if ((c?["tema"]?.GetValue<string>().Length ?? 0) > 120)
            {
                erros.Add($"novidadeId '{id}': tema acima de 120 caracteres.");
            }

            if ((c?["justificativa"]?.GetValue<string>().Length ?? 0) > 500)
            {
                erros.Add($"novidadeId '{id}': justificativa acima de 500 caracteres.");
            }
        }

        foreach (var faltante in esperados.Except(vistos))
        {
            erros.Add($"novidadeId '{faltante}' da entrada não foi classificado.");
        }

        return erros;
    }
}
