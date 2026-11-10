using Agente.Domain.Comum;

namespace Agente.Application.Sofia;

/// <summary>
/// Regras do contrato SOFIA v1.0 que o JSON Schema não expressa sozinho: UUIDs, limites configurados,
/// ids de documento únicos, correlação entre pedido e resposta e coerência entre status e erro.
/// Mensagens de erro nunca repetem conteúdo de documento nem o nome do usuário.
/// </summary>
public sealed class ValidadorContratoSofia(OpcoesContratoSofia opcoes)
{
    public Resultado<SolicitacaoAnaliseSofia> ValidarSolicitacao(SolicitacaoAnaliseSofia? s)
    {
        if (s is null)
        {
            return Resultado<SolicitacaoAnaliseSofia>.Falha("contrato.vazio", "Solicitação ausente.");
        }

        var erros = new List<ErroDominio>();

        if (s.VersaoContrato != ContratoSofia.Versao)
        {
            erros.Add(new("contrato.versao", $"versaoContrato deve ser \"{ContratoSofia.Versao}\"."));
        }

        ExigirUuid(erros, s.IdExecucao, "idExecucao");
        ExigirUuid(erros, s.IdSolicitacao, "idSolicitacao");

        if (string.IsNullOrWhiteSpace(s.Usuario?.Nome))
        {
            erros.Add(new("contrato.usuario", "usuario.nome é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(s.Pergunta))
        {
            erros.Add(new("contrato.pergunta", "pergunta é obrigatória."));
        }
        else if (s.Pergunta.Length > opcoes.MaxCaracteresPergunta)
        {
            erros.Add(new("contrato.limite", $"pergunta excede {opcoes.MaxCaracteresPergunta} caracteres."));
        }

        ValidarDocumentos(erros, s.Documentos);

        return erros.Count == 0 ? Resultado<SolicitacaoAnaliseSofia>.Ok(s) : Resultado<SolicitacaoAnaliseSofia>.Falha(erros);
    }

    /// <summary>Confere a resposta antes de enviá-la: mesmos ids, este agente e referências existentes.</summary>
    public Resultado ValidarResultado(ResultadoAnaliseSofia? r, SolicitacaoAnaliseSofia solicitacao)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        if (r is null)
        {
            return Resultado.Falha("contrato.vazio", "Resultado ausente.");
        }

        var erros = new List<ErroDominio>();

        if (r.VersaoContrato != ContratoSofia.Versao)
        {
            erros.Add(new("contrato.versao", $"versaoContrato deve ser \"{ContratoSofia.Versao}\"."));
        }

        if (r.IdExecucao != solicitacao.IdExecucao || r.IdSolicitacao != solicitacao.IdSolicitacao)
        {
            erros.Add(new("contrato.correlacao", "idExecucao e idSolicitacao devem ser os mesmos da solicitação."));
        }

        if (r.Agente is null || r.Agente.IdAgente != opcoes.IdAgente || string.IsNullOrWhiteSpace(r.Agente.Nome))
        {
            erros.Add(new("contrato.agente", $"agente.idAgente deve ser \"{opcoes.IdAgente}\" e agente.nome é obrigatório."));
        }

        if (!StatusAnaliseSofia.Todos.Contains(r.Status))
        {
            erros.Add(new("contrato.status", "status deve ser concluido, parcial ou erro."));
        }

        if (string.IsNullOrWhiteSpace(r.Analise))
        {
            erros.Add(new("contrato.analise", "analise é obrigatória, mesmo sem achados."));
        }

        if (r.Status == StatusAnaliseSofia.Erro && (r.Erro is null || string.IsNullOrWhiteSpace(r.Erro.Codigo) || string.IsNullOrWhiteSpace(r.Erro.Mensagem)))
        {
            erros.Add(new("contrato.erro", "status erro exige erro.codigo e erro.mensagem."));
        }
        else if (r.Status != StatusAnaliseSofia.Erro && r.Erro is not null)
        {
            erros.Add(new("contrato.erro", "erro só pode ser preenchido com status erro."));
        }

        ValidarAchados(erros, r.Achados, solicitacao);

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falha(erros);
    }

    private void ValidarDocumentos(List<ErroDominio> erros, IReadOnlyList<DocumentoSofia>? documentos)
    {
        if (documentos is null || documentos.Count == 0)
        {
            erros.Add(new("contrato.documentos", "documentos deve ter ao menos um item."));
            return;
        }

        if (documentos.Count > opcoes.MaxDocumentos)
        {
            erros.Add(new("contrato.limite", $"documentos excede {opcoes.MaxDocumentos} itens."));
        }

        long total = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < documentos.Count; i++)
        {
            var d = documentos[i];
            var campo = $"documentos[{i}]";
            if (d is null)
            {
                erros.Add(new("contrato.documento", $"{campo} é nulo."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(d.IdDocumento))
            {
                erros.Add(new("contrato.documento", $"{campo}.idDocumento é obrigatório."));
            }
            else if (!ids.Add(d.IdDocumento))
            {
                erros.Add(new("contrato.documento", $"{campo}.idDocumento repetido."));
            }

            if (string.IsNullOrWhiteSpace(d.Nome))
            {
                erros.Add(new("contrato.documento", $"{campo}.nome é obrigatório."));
            }

            if (string.IsNullOrWhiteSpace(d.Texto))
            {
                erros.Add(new("contrato.documento", $"{campo}.texto é obrigatório."));
                continue;
            }

            total += d.Texto.Length;
            if (d.Texto.Length > opcoes.MaxCaracteresPorDocumento)
            {
                erros.Add(new("contrato.limite", $"{campo}.texto excede {opcoes.MaxCaracteresPorDocumento} caracteres."));
            }
        }

        if (total > opcoes.MaxCaracteresTotalDocumentos)
        {
            erros.Add(new("contrato.limite", $"A soma dos textos excede {opcoes.MaxCaracteresTotalDocumentos} caracteres."));
        }
    }

    private static void ValidarAchados(List<ErroDominio> erros, IReadOnlyList<AchadoSofia>? achados, SolicitacaoAnaliseSofia solicitacao)
    {
        if (achados is null)
        {
            erros.Add(new("contrato.achados", "achados é obrigatório (use [] quando não houver)."));
            return;
        }

        var documentos = (solicitacao.Documentos ?? []).Select(d => d.IdDocumento).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < achados.Count; i++)
        {
            var a = achados[i];
            var campo = $"achados[{i}]";
            if (string.IsNullOrWhiteSpace(a.IdAchado) || string.IsNullOrWhiteSpace(a.Descricao))
            {
                erros.Add(new("contrato.achado", $"{campo}.idAchado e {campo}.descricao são obrigatórios."));
            }

            if (a.Severidade is not null && !SeveridadeSofia.Todas.Contains(a.Severidade))
            {
                erros.Add(new("contrato.achado", $"{campo}.severidade deve ser baixa, media, alta ou critica."));
            }

            if ((a.IdDocumentos ?? []).Any(id => !documentos.Contains(id)))
            {
                erros.Add(new("contrato.referencia", $"{campo}.idDocumentos cita documento que não veio na solicitação."));
            }
        }
    }

    private static void ExigirUuid(List<ErroDominio> erros, string? valor, string campo)
    {
        if (!Guid.TryParse(valor, out _))
        {
            erros.Add(new("contrato.id", $"{campo} deve ser um UUID."));
        }
    }
}
