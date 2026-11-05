using Agente.Application.Portas;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agente.Infrastructure.Persistencia;

internal sealed class UnidadeDeTrabalho(AgenteDbContext db) : IUnidadeDeTrabalho
{
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

/// <summary>Bloqueio consultivo do PostgreSQL (pg_try_advisory_lock) numa conexão dedicada.</summary>
internal sealed class BloqueioExecucaoPostgres(NpgsqlDataSource dataSource) : IBloqueioExecucao
{
    public async Task<IAsyncDisposable?> TentarAdquirirAsync(string nome, CancellationToken ct)
    {
        var conexao = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtext(@nome))", conexao);
            cmd.Parameters.AddWithValue("nome", nome);
            var obtido = (bool)(await cmd.ExecuteScalarAsync(ct))!;
            if (obtido)
            {
                return new Bloqueio(conexao, nome);
            }
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }

        await conexao.DisposeAsync();
        return null;
    }

    private sealed class Bloqueio(NpgsqlConnection conexao, string nome) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtext(@nome))", conexao);
                cmd.Parameters.AddWithValue("nome", nome);
                await cmd.ExecuteScalarAsync();
            }
            finally
            {
                await conexao.DisposeAsync();
            }
        }
    }
}

/// <summary>Relógio do sistema no fuso de negócio (Boletim:Fuso).</summary>
internal sealed class RelogioSistema(TimeProvider tempo, TimeZoneInfo fuso) : IRelogio
{
    public DateTimeOffset AgoraUtc => tempo.GetUtcNow();

    public DateTimeOffset ParaLocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, fuso);

    public DateOnly HojeLocal => DateOnly.FromDateTime(ParaLocal(AgoraUtc).DateTime);
}

/// <summary>Cria a função imutável usada pela busca textual e o índice GIN correspondente.</summary>
internal static class EsquemaBusca
{
    public const string Funcao = "agente_unaccent";

    /// <summary>Expressão indexada; a busca deve usar exatamente o mesmo texto.</summary>
    public const string ExpressaoDocumento =
        "to_tsvector('portuguese'::regconfig, agente_unaccent(coalesce(ementa,'') || ' ' || coalesce(tipo_ato,'') || ' ' || coalesce(numero_ato,'') || ' ' || coalesce(orgao_emissor,'') || ' ' || coalesce(tema,'')))";

    public const string SqlCriacao =
        """
        CREATE EXTENSION IF NOT EXISTS unaccent;
        CREATE OR REPLACE FUNCTION agente_unaccent(text) RETURNS text
            LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
            AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;
        CREATE INDEX IF NOT EXISTS ix_novidades_busca ON novidades USING GIN (
        """ + ExpressaoDocumento + "\n);";

    public static Task GarantirAsync(AgenteDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(SqlCriacao, ct);
}
