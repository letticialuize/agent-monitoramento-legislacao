using Agente.Application.Comum;
using Agente.Domain.Comum;
using Agente.Domain.Operacao;

namespace Agente.Contract.Tests;

/// <summary>
/// Coerência entre os enums do domínio e os nomes técnicos de contracts/api-agente.openapi.yaml e
/// do envelope. O OpenAPI é lido como texto para não adicionar dependência de parser YAML.
/// </summary>
public class ConsultasContractTests
{
    private static readonly string OpenApi = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "api-agente.openapi.yaml"));
    private static readonly string Envelope = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "envelope-resposta.schema.json"));

    public static TheoryData<string> Esferas() => [.. Enum.GetValues<Esfera>().Select(e => Enumeracao.ParaContrato(e))];

    public static TheoryData<string> Perfis() => [.. Enum.GetValues<Perfil>().Select(e => Enumeracao.ParaContrato(e))];

    public static TheoryData<string> Grupos() => [.. Enum.GetValues<Grupo>().Select(e => Enumeracao.ParaContrato(e))];

    public static TheoryData<string> Categorias() => [.. Enum.GetValues<CategoriaAchado>().Select(e => Enumeracao.ParaContrato(e))];

    public static TheoryData<string> Status() => [.. Enum.GetValues<StatusProcessamento>().Select(e => Enumeracao.ParaContrato(e))];

    [Theory]
    [MemberData(nameof(Esferas))]
    [MemberData(nameof(Perfis))]
    [MemberData(nameof(Grupos))]
    public void Filtros_da_consulta_existem_no_openapi(string nome) => Assert.Contains(nome, OpenApi, StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(Categorias))]
    [MemberData(nameof(Status))]
    public void Categorias_e_status_existem_no_envelope(string nome) => Assert.Contains($"\"{nome}\"", Envelope, StringComparison.Ordinal);

    [Fact]
    public void ConsultaRequest_proibe_campos_extras_e_limita_assunto()
    {
        var inicio = OpenApi.IndexOf("ConsultaRequest:", StringComparison.Ordinal);
        var trecho = OpenApi[inicio..OpenApi.IndexOf("ClienteInput:", inicio, StringComparison.Ordinal)];
        Assert.Contains("additionalProperties: false", trecho, StringComparison.Ordinal);
        Assert.Contains("minLength: 2, maxLength: 200", trecho, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("consultor_tributario", Perfil.ConsultorTributario)]
    [InlineData("ConsultorTributario", Perfil.ConsultorTributario)]
    [InlineData("desenvolvedor", Perfil.Desenvolvedor)]
    public void Nomes_tecnicos_sao_lidos(string texto, Perfil esperado)
    {
        Assert.True(Enumeracao.TentarLer<Perfil>(texto, out var p));
        Assert.Equal(esperado, p);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("consultor")]
    [InlineData("")]
    public void Valores_fora_do_contrato_sao_recusados(string texto) => Assert.False(Enumeracao.TentarLer<Perfil>(texto, out _));
}
