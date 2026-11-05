using System.Text.Json;
using Agente.Application.Comum;
using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Monitoramento;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Agente.Infrastructure.Persistencia.Configuracoes;

/// <summary>Conversões de objetos de valor e listas para colunas simples (sem atributos no domínio).</summary>
internal static class Conversores
{
    public static readonly ValueConverter<CodigoFonte, string> CodigoFonte =
        new(v => v.Valor, s => Domain.Catalogo.ObjetosDeValor.CodigoFonte.Criar(s).Valor);

    public static readonly ValueConverter<LinkOficial, string> LinkOficial =
        new(v => v.Url, s => Domain.Catalogo.ObjetosDeValor.LinkOficial.Criar(s).Valor);

    public static readonly ValueConverter<Ente?, string?> Ente =
        new(v => v == null ? null : v.ToString(), s => s == null ? null : Domain.Catalogo.ObjetosDeValor.Ente.Parse(s).Valor);

    public static readonly ValueConverter<Ente, string> EnteObrigatorio =
        new(v => v.ToString(), s => Domain.Catalogo.ObjetosDeValor.Ente.Parse(s).Valor);

    public static readonly ValueConverter<ChaveDeduplicacao, string> Chave =
        new(v => v.Valor, s => ChaveDeduplicacao.DeValor(s));

    /// <summary>Lista serializada como jsonb (enums em snake_case).</summary>
    public static ValueConverter<IReadOnlyList<T>, string> ListaJson<T>() =>
        new(
            v => JsonSerializer.Serialize(v, JsonContrato.Opcoes),
            s => JsonSerializer.Deserialize<List<T>>(s, JsonContrato.Opcoes) ?? new List<T>());

    public static ValueComparer<IReadOnlyList<T>> ComparadorLista<T>() =>
        new(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e == null ? 0 : e.GetHashCode())),
            v => v.ToList());
}
