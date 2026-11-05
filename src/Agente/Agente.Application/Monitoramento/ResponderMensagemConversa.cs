using System.Globalization;
using System.Text.RegularExpressions;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Ferramentas;
using Agente.Application.Llm;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConsultaAgregado = Agente.Domain.Operacao.Consulta;

namespace Agente.Application.Monitoramento;

public sealed record RespostaMensagem(string Mensagem, EnvelopeResposta Envelope);

/// <summary>
/// Chat de consulta de novidades (US1). O LLM conversa e chama ferramentas somente leitura; a
/// resposta só é aceita se citar apenas ids e links retornados pelas ferramentas (Princípios V, XI).
/// </summary>
public sealed partial class ResponderMensagemConversa(
    IOrquestradorConversa orquestrador,
    FerramentasNovidades ferramentas,
    IRepositorioConversas memoria,
    IRepositorioConsultas consultas,
    IConsultaConsumo consumo,
    IUnidadeDeTrabalho unidade,
    RegistradorEtapas etapas,
    IRelogio relogio,
    IOptions<OpcoesMemoria> opcoesMemoria,
    IOptions<OpcoesValidacaoLlm> opcoesValidacao,
    IOptions<OpcoesConsulta> opcoesConsulta,
    ILogger<ResponderMensagemConversa> logger)
{
    internal const string PromptSistema =
        ConteudoNaoConfiavel.RegrasBase +
        """

        Você é o agente de monitoramento de legislação e jurisprudência da Infis.
        - Para qualquer pergunta sobre novidades, use a ferramenta buscar_novidades; para detalhar, use obter_novidade.
        - Responda em português, de forma objetiva, separando as novidades por perfil (Consultor tributário / Desenvolvedor) e por esfera.
        - Cite cada novidade com órgão, tipo e número do ato, data de publicação e o link exatamente como retornado pela ferramenta.
        - Indique quando uma novidade requer revisão humana, está pendente de confirmação ou vem de fonte fora do catálogo.
        - Informe fontes com falha e itens omitidos quando a ferramenta os retornar.
        - Se a ferramenta não retornar novidades, diga que não foram encontradas novidades nas fontes consultadas, sem afirmar que não houve mudança normativa.
        - Esta versão não consulta dados de clientes nem envia e-mails.
        """;

    public async Task<Resultado<RespostaMensagem>> ExecutarAsync(
        string conversaId,
        string texto,
        ContextoUsuario usuario,
        string correlacaoId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length is < 1 or > 4000)
        {
            return Resultado<RespostaMensagem>.Falha("conversa.texto_invalido", "A mensagem deve ter entre 1 e 4000 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(conversaId) || conversaId.Length > 100)
        {
            return Resultado<RespostaMensagem>.Falha("conversa.id_invalido", "Identificador de conversa inválido.");
        }

        var iniciado = relogio.AgoraUtc;
        var periodo = PeriodoConsulta.UltimosDias(relogio.HojeLocal, opcoesConsulta.Value.PeriodoPadraoDias);
        var assunto = texto.Length > 200 ? texto[..200] : texto;
        var consulta = ConsultaAgregado.Iniciar(correlacaoId, usuario.UsuarioId, conversaId, assunto, periodo, [], [], [], EnvelopeResposta.VersaoAtual, iniciado);
        consultas.Adicionar(consulta);

        if (!orquestrador.Configurado)
        {
            etapas.NaoExecutada(consulta, EtapaFluxo.Llm, "LLM não configurado.");
            return Resultado<RespostaMensagem>.Ok(await Encerrar(
                consulta, iniciado, correlacaoId, StatusProcessamento.FalhaTecnica,
                "O agente de conversa está indisponível no momento (LLM não configurado). Use a consulta estruturada.",
                new ColetorFerramentas(),
                [new Achado(CategoriaAchado.FalhaTecnica, "LLM não configurado.", null)],
                ["Conversa não executada: LLM não configurado."],
                ct));
        }

        var historico = await memoria.ObterUltimasAsync(usuario.UsuarioId, conversaId, Math.Max(0, opcoesMemoria.Value.MaxMensagens), ct);
        var contexto = new ContextoExecucao(consulta.Id, correlacaoId);
        var maxTentativas = 1 + Math.Max(0, opcoesValidacao.Value.RetentativasValidacao);
        var mensagem = texto;
        string? erroValidacao = null;

        for (var tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            var coletor = new ColetorFerramentas();
            var definicoes = ferramentas.Criar(usuario, correlacaoId, conversaId, consulta, coletor);

            var etapaLlm = etapas.Iniciar(consulta, EtapaFluxo.Llm);
            var resposta = await orquestrador.ResponderAsync(
                new RequisicaoConversa(contexto, PromptSistema, historico, mensagem, definicoes), ct);
            etapas.Concluir(etapaLlm, resposta.Sucesso ? StatusEtapa.Sucesso : StatusEtapa.Falha, resposta.Erro);

            if (!resposta.Sucesso || string.IsNullOrWhiteSpace(resposta.Texto))
            {
                return Resultado<RespostaMensagem>.Ok(await Encerrar(
                    consulta, iniciado, correlacaoId, StatusProcessamento.FalhaTecnica,
                    "Não foi possível obter resposta do agente agora. Tente novamente ou use a consulta estruturada.",
                    coletor,
                    [new Achado(CategoriaAchado.FalhaTecnica, $"Falha no LLM: {resposta.Erro}", null)],
                    ["Resposta do LLM indisponível."],
                    ct));
            }

            var etapaValidacao = etapas.Iniciar(consulta, EtapaFluxo.ValidacaoSaida);
            var violacoes = ValidarCitacoes(resposta.Texto, coletor);
            if (violacoes.Count == 0)
            {
                etapas.Concluir(etapaValidacao, StatusEtapa.Sucesso);
                await memoria.AcrescentarAsync(usuario.UsuarioId, conversaId,
                [
                    new MensagemConversa("usuario", texto, iniciado),
                    new MensagemConversa("assistente", resposta.Texto, relogio.AgoraUtc),
                ], ct);

                var envelopes = coletor.Envelopes;
                var achados = envelopes.SelectMany(e => e.Achados).Distinct().ToList();
                var limitacoes = envelopes.SelectMany(e => e.Limitacoes).Distinct().ToList();
                var status = envelopes.Any(e => e.Status != StatusProcessamento.Concluido) || limitacoes.Count > 0
                    ? StatusProcessamento.ConcluidoComLimitacoes
                    : StatusProcessamento.Concluido;
                if (envelopes.Count == 0)
                {
                    limitacoes.Add("O agente respondeu sem consultar a base de novidades.");
                }

                return Resultado<RespostaMensagem>.Ok(await Encerrar(
                    consulta, iniciado, correlacaoId, status, resposta.Texto, coletor, achados, limitacoes, ct));
            }

            erroValidacao = string.Join("; ", violacoes);
            etapas.Concluir(etapaValidacao, StatusEtapa.Falha, erroValidacao);
            logger.LogWarning("Resposta da conversa rejeitada (tentativa {Tentativa}, execução {ExecucaoId}): {Erros}", tentativa, consulta.Id, erroValidacao);
            mensagem = texto + "\n\n[Correção obrigatória] A resposta anterior citou links ou ids que não foram retornados pelas ferramentas: "
                + erroValidacao + ". Use somente dados retornados pelas ferramentas.";
        }

        return Resultado<RespostaMensagem>.Ok(await Encerrar(
            consulta, iniciado, correlacaoId, StatusProcessamento.FalhaTecnica,
            "Não foi possível gerar uma resposta verificável. Use a consulta estruturada para ver as novidades com evidências.",
            new ColetorFerramentas(),
            [new Achado(CategoriaAchado.FalhaTecnica, $"Resposta do LLM reprovada na validação: {erroValidacao}", null)],
            ["A resposta do LLM citou evidências não retornadas pelas ferramentas."],
            ct));
    }

    /// <summary>Links e ids citados na resposta devem ter sido retornados pelas ferramentas.</summary>
    internal static List<string> ValidarCitacoes(string texto, ColetorFerramentas coletor)
    {
        var violacoes = new List<string>();
        var urls = coletor.UrlsRetornadas;
        foreach (Match m in Url().Matches(texto))
        {
            var url = m.Value.TrimEnd('.', ',', ';', ':', ')', ']', '>', '"', '\'');
            if (!urls.Contains(url))
            {
                violacoes.Add($"link não retornado: {url}");
            }
        }

        var ids = coletor.Novidades.Select(n => n.Id).ToHashSet();
        foreach (Match m in Guid().Matches(texto))
        {
            if (System.Guid.TryParse(m.Value, CultureInfo.InvariantCulture, out var g) && !ids.Contains(g))
            {
                violacoes.Add($"id não retornado: {m.Value}");
            }
        }

        return violacoes.Distinct().ToList();
    }

    private async Task<RespostaMensagem> Encerrar(
        ConsultaAgregado consulta,
        DateTimeOffset iniciado,
        string correlacaoId,
        StatusProcessamento status,
        string mensagem,
        ColetorFerramentas coletor,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<string> limitacoes,
        CancellationToken ct)
    {
        etapas.Concluir(etapas.Iniciar(consulta, EtapaFluxo.Resposta), status == StatusProcessamento.FalhaTecnica ? StatusEtapa.Falha : StatusEtapa.Sucesso);
        var novidades = coletor.Novidades.ToList();
        var requerRevisao = novidades.Any(n => n.RequerRevisaoHumana);
        var finalizado = relogio.AgoraUtc;
        consulta.Finalizar(status, requerRevisao, limitacoes, finalizado);
        await unidade.SalvarAsync(ct);

        var ultimo = coletor.Envelopes.LastOrDefault()?.Resultado;
        ResultadoConsulta? resultado = ultimo is null
            ? null
            : ultimo with { Novidades = novidades, FontesComFalha = coletor.Envelopes.SelectMany(e => e.Resultado!.FontesComFalha).Distinct().ToList() };

        return new RespostaMensagem(mensagem, new EnvelopeResposta(
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

    [GeneratedRegex(@"https?://[^\s<>""'\)\]]+", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex Guid();
}
