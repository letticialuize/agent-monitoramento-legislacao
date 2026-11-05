using Agente.Application.Comum;
using Agente.Application.Portas;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Agente.Infrastructure.Memoria;

/// <summary>
/// Memória conversacional em MongoDB (ADR 0001, Princípio XIV): isolada por usuário e conversa,
/// com expiração por TTL e leitura limitada às últimas mensagens.
/// </summary>
internal sealed class RepositorioConversas : IRepositorioConversas
{
    private const int MaximoArmazenado = 200;
    private readonly IMongoCollection<DocumentoConversa> _colecao;
    private readonly Lazy<Task> _indices;

    public RepositorioConversas(IMongoClient cliente, IOptions<OpcoesMemoria> opcoes)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(opcoes);
        _colecao = cliente.GetDatabase(opcoes.Value.BancoMongo).GetCollection<DocumentoConversa>("conversas");
        _indices = new Lazy<Task>(() => CriarIndicesAsync(TimeSpan.FromDays(Math.Max(1, opcoes.Value.TtlDias))));
    }

    public async Task<IReadOnlyList<MensagemConversa>> ObterUltimasAsync(string usuarioId, string conversaId, int maximo, CancellationToken ct)
    {
        await _indices.Value;
        if (maximo <= 0)
        {
            return [];
        }

        var doc = await _colecao
            .Find(d => d.UsuarioId == usuarioId && d.ConversaId == conversaId)
            .FirstOrDefaultAsync(ct);

        return doc?.Mensagens
            .TakeLast(maximo)
            .Select(m => new MensagemConversa(m.Papel, m.Texto, m.Em))
            .ToList() ?? [];
    }

    public async Task AcrescentarAsync(string usuarioId, string conversaId, IReadOnlyList<MensagemConversa> mensagens, CancellationToken ct)
    {
        await _indices.Value;
        var novas = mensagens.Select(m => new DocumentoMensagem { Papel = m.Papel, Texto = m.Texto, Em = m.Em.UtcDateTime }).ToList();
        var update = Builders<DocumentoConversa>.Update
            .PushEach(d => d.Mensagens, novas, slice: -MaximoArmazenado)
            .Set(d => d.AtualizadoEm, DateTime.UtcNow)
            .SetOnInsert(d => d.UsuarioId, usuarioId)
            .SetOnInsert(d => d.ConversaId, conversaId);

        await _colecao.UpdateOneAsync(
            d => d.UsuarioId == usuarioId && d.ConversaId == conversaId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    private async Task CriarIndicesAsync(TimeSpan ttl)
    {
        await _colecao.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<DocumentoConversa>(
                Builders<DocumentoConversa>.IndexKeys.Ascending(d => d.UsuarioId).Ascending(d => d.ConversaId),
                new CreateIndexOptions { Unique = true, Name = "ux_usuario_conversa" }),
            new CreateIndexModel<DocumentoConversa>(
                Builders<DocumentoConversa>.IndexKeys.Ascending(d => d.AtualizadoEm),
                new CreateIndexOptions { ExpireAfter = ttl, Name = "ttl_atualizado_em" }),
        ]);
    }

    internal sealed class DocumentoConversa
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("usuarioId")]
        public string UsuarioId { get; set; } = string.Empty;

        [BsonElement("conversaId")]
        public string ConversaId { get; set; } = string.Empty;

        [BsonElement("mensagens")]
        public List<DocumentoMensagem> Mensagens { get; set; } = [];

        [BsonElement("atualizadoEm")]
        public DateTime AtualizadoEm { get; set; }
    }

    internal sealed class DocumentoMensagem
    {
        [BsonElement("papel")]
        public string Papel { get; set; } = string.Empty;

        [BsonElement("texto")]
        public string Texto { get; set; } = string.Empty;

        [BsonElement("em")]
        public DateTime Em { get; set; }
    }
}
