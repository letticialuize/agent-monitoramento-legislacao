using System.Text;
using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agente.Infrastructure.Persistencia.Consultas;

/// <summary>Busca textual (portuguese + unaccent) sobre novidades (research R-04).</summary>
internal sealed class ConsultaNovidades(AgenteDbContext db) : IConsultaNovidades
{
    private sealed record Linha(Guid Id, long Total);

    public async Task<PaginaNovidades> BuscarAsync(FiltroNovidades filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var onde = new StringBuilder(
            """
            n.data_publicacao BETWEEN @inicio AND @fim
              AND n.situacao <> 'Descartada'
            """);

        if (!filtro.IncluirPendentes)
        {
            onde.AppendLine().Append("  AND n.situacao = 'Confirmada'");
        }

        var termos = filtro.Termos.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (termos.Count > 0)
        {
            var consultas = termos.Select((_, i) => $"plainto_tsquery('portuguese'::regconfig, agente_unaccent(@t{i}))");
            onde.AppendLine().Append("  AND ").Append(EsquemaBusca.ExpressaoDocumento).Append(" @@ (").Append(string.Join(" || ", consultas)).Append(')');
        }

        if (filtro.Esferas.Count > 0)
        {
            onde.AppendLine().Append("  AND n.esfera = ANY(@esferas)");
        }

        if (filtro.Grupos.Count > 0)
        {
            onde.AppendLine().Append("  AND n.grupo = ANY(@grupos)");
        }

        if (filtro.Perfis.Count > 0)
        {
            onde.AppendLine().Append("  AND n.perfis ?| @perfis");
        }

        // Um parâmetro do Npgsql não pode ser usado em dois comandos: cada consulta recebe os seus.
        object[] Parametros(bool comLimite)
        {
            var p = new List<NpgsqlParameter> { new("inicio", filtro.Inicio), new("fim", filtro.Fim) };
            p.AddRange(termos.Select((t, i) => new NpgsqlParameter($"t{i}", t)));
            if (filtro.Esferas.Count > 0)
            {
                p.Add(new NpgsqlParameter("esferas", filtro.Esferas.Select(e => e.ToString()).ToArray()));
            }

            if (filtro.Grupos.Count > 0)
            {
                p.Add(new NpgsqlParameter("grupos", filtro.Grupos.Select(e => e.ToString()).ToArray()));
            }

            if (filtro.Perfis.Count > 0)
            {
                p.Add(new NpgsqlParameter("perfis", filtro.Perfis.Select(Enumeracao.ParaContrato).ToArray()));
            }

            if (comLimite)
            {
                p.Add(new NpgsqlParameter("limite", filtro.Limite));
            }

            return [.. p];
        }

        var ocultadas = 0;
        if (!filtro.IncluirNaoTributarias)
        {
            var sqlOcultadas = $"SELECT count(*)::int AS \"Value\" FROM novidades n WHERE {onde} AND n.relacionada_tributos IS FALSE";
            ocultadas = await db.Database.SqlQueryRaw<int>(sqlOcultadas, Parametros(false)).SingleAsync(ct);
            onde.AppendLine().Append("  AND n.relacionada_tributos IS NOT FALSE");
        }

        var sql =
            $"""
            SELECT n.id AS "Id", count(*) OVER () AS "Total"
            FROM novidades n
            WHERE {onde}
            ORDER BY n.data_publicacao DESC, n.criado_em DESC LIMIT @limite
            """;

        var linhas = await db.Database
            .SqlQueryRaw<Linha>(sql, Parametros(true))
            .ToListAsync(ct);

        if (linhas.Count == 0)
        {
            return new PaginaNovidades([], 0, ocultadas);
        }

        var ids = linhas.Select(l => l.Id).ToList();
        var novidades = await db.Novidades.AsNoTracking().Where(n => ids.Contains(n.Id)).ToListAsync(ct);
        var codigos = await CodigosFontesAsync(novidades, ct);
        var porId = novidades.ToDictionary(n => n.Id);
        var itens = ids.Where(porId.ContainsKey).Select(id => Mapear(porId[id], codigos)).ToList();
        return new PaginaNovidades(itens, (int)linhas[0].Total, ocultadas);
    }

    public async Task<NovidadeDto?> ObterAsync(Guid id, CancellationToken ct)
    {
        var n = await db.Novidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Situacao != SituacaoNovidade.Descartada, ct);
        return n is null ? null : Mapear(n, await CodigosFontesAsync([n], ct));
    }

    private async Task<Dictionary<Guid, string>> CodigosFontesAsync(IEnumerable<Novidade> novidades, CancellationToken ct)
    {
        var fontes = novidades.SelectMany(n => n.Ocorrencias).Where(o => o.FonteId != null).Select(o => o.FonteId!.Value).Distinct().ToList();
        if (fontes.Count == 0)
        {
            return [];
        }

        var lista = await db.Fontes.AsNoTracking().Where(f => fontes.Contains(f.Id)).Select(f => new { f.Id, f.Codigo }).ToListAsync(ct);
        return lista.ToDictionary(f => f.Id, f => f.Codigo.Valor);
    }

    internal static NovidadeDto Mapear(Novidade n, IReadOnlyDictionary<Guid, string> codigos)
    {
        var evidencias = n.Ocorrencias
            .OrderByDescending(o => o.Principal)
            .ThenBy(o => o.ColetadaEm)
            .Select(o => new EvidenciaDto(
                o.FonteId is { } f && codigos.TryGetValue(f, out var c) ? c : null,
                o.Url,
                o.Principal))
            .ToList();

        // Confirmação por revisão humana: o link oficial informado passa a ser a evidência principal.
        if (n.LinkOficial is not null && evidencias.All(e => !string.Equals(e.Url, n.LinkOficial, StringComparison.Ordinal)))
        {
            evidencias = evidencias.Select(e => e with { Principal = false }).Prepend(new EvidenciaDto(null, n.LinkOficial, true)).ToList();
        }

        return new NovidadeDto(
            n.Id,
            n.Situacao,
            n.Esfera,
            n.Ente?.ToString(),
            n.Perfis,
            n.OrigemPerfil,
            n.DataPublicacao,
            n.OrgaoEmissor,
            n.TipoAto,
            n.NumeroAto,
            n.Ementa,
            n.Tema,
            n.Impacto,
            n.OrigemImpacto,
            null,
            n.RetificaNovidadeId,
            evidencias,
            n.RequerRevisaoHumana,
            n.RelacionadaTributos);
    }
}

/// <summary>Fontes do catálogo vigente que cobrem o filtro e sua situação de coleta.</summary>
internal sealed class ConsultaSituacaoFontes(AgenteDbContext db) : IConsultaSituacaoFontes
{
    public async Task<SituacaoFontes> ObterAsync(DateOnly inicio, DateOnly fim, IReadOnlyList<Esfera> esferas, IReadOnlyList<Grupo> grupos, CancellationToken ct)
    {
        var vigente = await db.VersoesCatalogo.AsNoTracking().FirstOrDefaultAsync(v => v.Vigente, ct);
        if (vigente is null)
        {
            return new SituacaoFontes([], [], []);
        }

        var fontes = vigente.FontesAtivas()
            .Where(f => grupos.Count == 0 || grupos.Contains(f.Grupo))
            .Where(f => esferas.Count == 0 || f.Esfera == EsferaFonte.Nacional || esferas.Any(e => e.ToString() == f.Esfera.ToString()))
            .ToList();

        var ids = fontes.Select(f => f.Id).ToList();
        var ultimas = await db.ExecucoesColeta.AsNoTracking()
            .Where(e => ids.Contains(e.FonteId) && e.Status != StatusExecucao.EmAndamento)
            .GroupBy(e => e.FonteId)
            .Select(g => g.OrderByDescending(e => e.IniciadaEm).First())
            .ToListAsync(ct);
        var porFonte = ultimas.ToDictionary(e => e.FonteId);

        var comFalha = new List<FonteComFalha>();
        var nunca = new List<string>();
        foreach (var f in fontes)
        {
            if (!porFonte.TryGetValue(f.Id, out var ultima))
            {
                nunca.Add(f.Codigo.Valor);
            }
            else if (ultima.Status == StatusExecucao.FalhaTecnica)
            {
                comFalha.Add(new FonteComFalha(f.Codigo.Valor, ultima.Erro ?? "falha técnica"));
            }
        }

        return new SituacaoFontes(fontes.Select(f => f.Codigo.Valor).ToList(), comFalha, nunca);
    }
}

internal sealed class ConsultaConsumo(AgenteDbContext db) : IConsultaConsumo
{
    public async Task<ResumoConsumo> ResumirAsync(Guid execucaoId, CancellationToken ct)
    {
        var registros = await db.RegistrosConsumo.AsNoTracking().Where(r => r.ExecucaoId == execucaoId).ToListAsync(ct);
        if (registros.Count == 0)
        {
            return ResumoConsumo.Nenhum;
        }

        var conhecido = registros.All(r => r.UsoConhecido);
        var custoConhecido = conhecido && registros.All(r => r.CustoEstimado is not null);
        return new ResumoConsumo(
            registros.Count,
            conhecido ? registros.Sum(r => r.TokensEntrada!.Value) : null,
            conhecido ? registros.Sum(r => r.TokensSaida!.Value) : null,
            conhecido,
            custoConhecido ? registros.Sum(r => r.CustoEstimado!.Value) : null,
            custoConhecido ? registros.Select(r => r.Moeda).FirstOrDefault() : null);
    }
}

/// <summary>Expurgo dos dados operacionais (constitution 4.3, FR-034).</summary>
internal sealed class ExpurgoOperacional(AgenteDbContext db) : IExpurgoOperacional
{
    public async Task<ResultadoExpurgo> ExpurgarAsync(DateTimeOffset limite, CancellationToken ct)
    {
        var r = new Dictionary<string, int>
        {
            ["consultas"] = await db.Consultas.Where(c => c.CriadoEm < limite).ExecuteDeleteAsync(ct),
            ["registros_consumo"] = await db.RegistrosConsumo.Where(c => c.CriadoEm < limite).ExecuteDeleteAsync(ct),
            ["execucoes_coleta"] = await db.ExecucoesColeta.Where(c => c.CriadoEm < limite).ExecuteDeleteAsync(ct),
            // Novidade em análise (revisão humana aberta) é mantida; ocorrências e revisões saem em cascata.
            ["novidades"] = await db.Novidades
                .Where(n => n.CriadoEm < limite && n.Situacao != SituacaoNovidade.EmAnalise)
                .ExecuteDeleteAsync(ct),
        };
        return new ResultadoExpurgo(r);
    }
}
