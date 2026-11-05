using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Agente.Application.Llm;

/// <summary>Contrato JSON versionado da saída do LLM (specs/.../contracts/llm-*.schema.json).</summary>
public sealed class ContratoLlm
{
    // Palavras-chave que o modo estrito de Structured Outputs pode não aceitar: são removidas do
    // schema enviado ao modelo e verificadas pela validação completa (research R-06).
    private static readonly string[] PalavrasRemovidasNoEstrito =
        ["maxLength", "minLength", "maxItems", "minItems", "uniqueItems", "$schema", "$id", "title", "description"];

    public string Nome { get; }

    public string SchemaCompleto { get; }

    public string SchemaEstrito { get; }

    public JsonSchema Schema { get; }

    internal ContratoLlm(string nome, string schemaCompleto)
    {
        Nome = nome;
        SchemaCompleto = schemaCompleto;
        Schema = JsonSchema.FromText(schemaCompleto);
        var no = JsonNode.Parse(schemaCompleto)!;
        Simplificar(no);
        SchemaEstrito = no.ToJsonString();
    }

    private static void Simplificar(JsonNode? no)
    {
        switch (no)
        {
            case JsonObject obj:
                foreach (var chave in PalavrasRemovidasNoEstrito)
                {
                    obj.Remove(chave);
                }

                foreach (var (_, filho) in obj.ToList())
                {
                    Simplificar(filho);
                }

                break;
            case JsonArray arr:
                foreach (var filho in arr)
                {
                    Simplificar(filho);
                }

                break;
        }
    }
}

/// <summary>Contratos embutidos no assembly a partir dos arquivos da especificação.</summary>
public static class ContratosLlm
{
    private static readonly Lazy<ContratoLlm> Classificacao = new(() => Carregar("llm-classificacao-novidade"));

    public static ContratoLlm ClassificacaoNovidade => Classificacao.Value;

    private static ContratoLlm Carregar(string nome)
    {
        var recurso = $"Contratos.{nome}.schema.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Contrato LLM não embutido: {recurso}");
        using var leitor = new StreamReader(stream);
        return new ContratoLlm(nome, leitor.ReadToEnd());
    }
}
