using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agente.Application.Comum;

/// <summary>Usuário autenticado que originou a operação.</summary>
public sealed record ContextoUsuario(string UsuarioId, IReadOnlyList<string> Papeis)
{
    public bool TemAlgumPapel(params string[] papeis) =>
        Papeis.Any(p => papeis.Contains(p, StringComparer.OrdinalIgnoreCase));
}

public static class Papeis
{
    public const string Consultor = "consultor";
    public const string Desenvolvedor = "desenvolvedor";
    public const string Revisor = "revisor";
    public const string Administrador = "administrador";
}

/// <summary>Serialização dos contratos: propriedades camelCase e enums snake_case.</summary>
public static class JsonContrato
{
    public static JsonSerializerOptions Opcoes { get; } = Criar();

    public static void Configurar(JsonSerializerOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        opcoes.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    }

    private static JsonSerializerOptions Criar()
    {
        var o = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        Configurar(o);
        o.MakeReadOnly();
        return o;
    }
}
