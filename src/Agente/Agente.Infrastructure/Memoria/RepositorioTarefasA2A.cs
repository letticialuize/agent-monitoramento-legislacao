using Agente.Application.Comum;
using Agente.Application.Portas;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Agente.Infrastructure.Memoria;

/// <summary>
/// Tarefas A2A em MongoDB (feature 006): a Task fica como JSON opaco, com o mesmo TTL da memória.
/// Compartilhado entre instâncias da Vercel, ao contrário de um cache em memória.
/// </summary>
internal sealed class RepositorioTarefasA2A : IRepositorioTarefasA2A
{
    private readonly IMongoCollection<DocumentoTarefa> _colecao;
    private readonly Lazy<Task> _indices;

    public RepositorioTarefasA2A(IMongoClient cliente, IOptions<OpcoesMemoria> opcoes)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(opcoes);
        _colecao = cliente.GetDatabase(opcoes.Value.BancoMongo).GetCollection<DocumentoTarefa>("tarefas_a2a");
        var ttl = TimeSpan.FromDays(Math.Max(1, opcoes.Value.TtlDias));
        _indices = new Lazy<Task>(() => _colecao.Indexes.CreateOneAsync(new CreateIndexModel<DocumentoTarefa>(
            Builders<DocumentoTarefa>.IndexKeys.Ascending(d => d.CriadoEm),
            new CreateIndexOptions { ExpireAfter = ttl, Name = "ttl_criado_em" })));
    }

    public async Task SalvarAsync(string idTarefa, string tarefaJson, CancellationToken ct)
    {
        await _indices.Value;
        await _colecao.ReplaceOneAsync(
            d => d.Id == idTarefa,
            new DocumentoTarefa { Id = idTarefa, Tarefa = tarefaJson, CriadoEm = DateTime.UtcNow },
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<string?> ObterAsync(string idTarefa, CancellationToken ct)
    {
        await _indices.Value;
        return (await _colecao.Find(d => d.Id == idTarefa).FirstOrDefaultAsync(ct))?.Tarefa;
    }

    internal sealed class DocumentoTarefa
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("tarefa")]
        public string Tarefa { get; set; } = string.Empty;

        [BsonElement("criadoEm")]
        public DateTime CriadoEm { get; set; }
    }
}
