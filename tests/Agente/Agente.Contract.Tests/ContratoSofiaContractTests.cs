using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Application.Comum;
using Agente.Application.Sofia;
using Json.Schema;

namespace Agente.Contract.Tests;

/// <summary>Contrato SOFIA v1.0 (feature 004): schemas, round-trip JSON e regras do validador.</summary>
public class ContratoSofiaContractTests
{
    private static readonly string Pasta = Path.Combine(AppContext.BaseDirectory, "ContratosSofia");
    private static readonly JsonSchema SchemaSolicitacao = JsonSchema.FromFile(Path.Combine(Pasta, "solicitacao-analise-sofia-v1.schema.json"));
    private static readonly JsonSchema SchemaResultado = JsonSchema.FromFile(Path.Combine(Pasta, "resultado-analise-sofia-v1.schema.json"));
    private static readonly OpcoesContratoSofia Opcoes = new();
    private static readonly ValidadorContratoSofia Validador = new(Opcoes);

    private static JsonObject Exemplo(string nome) => JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, "exemplos", nome)))!.AsObject();

    private static bool ValidaSchema(JsonSchema schema, JsonNode json) =>
        schema.Evaluate(JsonDocument.Parse(json.ToJsonString()).RootElement).IsValid;

    private static T Ler<T>(JsonNode json) => JsonSerializer.Deserialize<T>(json.ToJsonString(), JsonContrato.Opcoes)!;

    private static SolicitacaoAnaliseSofia SolicitacaoValida() => Ler<SolicitacaoAnaliseSofia>(Exemplo("solicitacao-valida.json"));

    // ---------- Solicitação ----------

    [Fact]
    public void Exemplo_de_solicitacao_valida_no_schema_e_no_validador()
    {
        var json = Exemplo("solicitacao-valida.json");

        Assert.True(ValidaSchema(SchemaSolicitacao, json));
        Assert.True(Validador.ValidarSolicitacao(Ler<SolicitacaoAnaliseSofia>(json)).Sucesso);
    }

    [Fact]
    public void Round_trip_preserva_ids_usuario_pergunta_e_documentos()
    {
        var original = SolicitacaoValida();

        var json = JsonSerializer.Serialize(original, JsonContrato.Opcoes);
        var lido = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(json, JsonContrato.Opcoes)!;

        Assert.True(ValidaSchema(SchemaSolicitacao, JsonNode.Parse(json)!));
        Assert.Equal(original.IdExecucao, lido.IdExecucao);
        Assert.Equal(original.IdSolicitacao, lido.IdSolicitacao);
        Assert.Equal(original.Usuario!.Nome, lido.Usuario!.Nome);
        Assert.Equal(original.Pergunta, lido.Pergunta);
        Assert.Equal(original.Documentos!.Select(d => d.IdDocumento), lido.Documentos!.Select(d => d.IdDocumento));
        Assert.Equal(original.Documentos!.Select(d => d.Texto), lido.Documentos!.Select(d => d.Texto));
    }

    [Fact]
    public void Campo_novo_desconhecido_e_tolerado_na_v1()
    {
        var json = Exemplo("solicitacao-valida.json");
        json["campoFuturo"] = "valor";
        json["usuario"]!["email"] = "ignorado@exemplo.com";

        Assert.True(ValidaSchema(SchemaSolicitacao, json));
        Assert.True(Validador.ValidarSolicitacao(Ler<SolicitacaoAnaliseSofia>(json)).Sucesso);
    }

    public static TheoryData<string, string> SolicitacoesInvalidas => new()
    {
        { "sem_versao", "contrato.versao" },
        { "versao_2", "contrato.versao" },
        { "idExecucao_nao_uuid", "contrato.id" },
        { "sem_idSolicitacao", "contrato.id" },
        { "usuario_sem_nome", "contrato.usuario" },
        { "sem_pergunta", "contrato.pergunta" },
        { "documentos_vazio", "contrato.documentos" },
        { "sem_documentos", "contrato.documentos" },
        { "documento_texto_vazio", "contrato.documento" },
        { "documento_sem_id", "contrato.documento" },
    };

    [Theory]
    [MemberData(nameof(SolicitacoesInvalidas))]
    public void Solicitacao_invalida_e_recusada_no_schema_e_no_validador(string caso, string codigoEsperado)
    {
        var json = Exemplo("solicitacao-valida.json");
        var docs = json["documentos"]!.AsArray();
        switch (caso)
        {
            case "sem_versao": json.Remove("versaoContrato"); break;
            case "versao_2": json["versaoContrato"] = "2.0"; break;
            case "idExecucao_nao_uuid": json["idExecucao"] = "exec-1"; break;
            case "sem_idSolicitacao": json.Remove("idSolicitacao"); break;
            case "usuario_sem_nome": json["usuario"]!["nome"] = "  "; break;
            case "sem_pergunta": json.Remove("pergunta"); break;
            case "documentos_vazio": docs.Clear(); break;
            case "sem_documentos": json.Remove("documentos"); break;
            case "documento_texto_vazio": docs[0]!["texto"] = ""; break;
            case "documento_sem_id": docs[0]!.AsObject().Remove("idDocumento"); break;
        }

        Assert.False(ValidaSchema(SchemaSolicitacao, json));
        var r = Validador.ValidarSolicitacao(Ler<SolicitacaoAnaliseSofia>(json));
        Assert.False(r.Sucesso);
        Assert.Contains(r.Erros, e => e.Codigo == codigoEsperado);
    }

    [Fact]
    public void IdDocumento_repetido_e_recusado()
    {
        var s = SolicitacaoValida();
        s = s with { Documentos = [s.Documentos![0], s.Documentos![0]] };

        Assert.Contains(Validador.ValidarSolicitacao(s).Erros, e => e.Codigo == "contrato.documento");
    }

    [Fact]
    public void Payload_acima_do_limite_e_recusado_sem_truncar()
    {
        var limitado = new ValidadorContratoSofia(new OpcoesContratoSofia { MaxCaracteresPorDocumento = 10, MaxCaracteresPergunta = 5 });

        var r = limitado.ValidarSolicitacao(SolicitacaoValida());

        Assert.False(r.Sucesso);
        // Pergunta + os dois documentos do exemplo.
        Assert.Equal(3, r.Erros.Count(e => e.Codigo == "contrato.limite"));
    }

    [Fact]
    public void Mensagens_de_erro_nao_repetem_nome_do_usuario_nem_texto_de_documento()
    {
        var s = SolicitacaoValida() with { VersaoContrato = "9", Pergunta = "" };

        var mensagens = string.Join("|", Validador.ValidarSolicitacao(s).Erros.Select(e => e.Mensagem));

        Assert.DoesNotContain("Maria", mensagens, StringComparison.Ordinal);
        Assert.DoesNotContain("NF-e de teste", mensagens, StringComparison.Ordinal);
    }

    // ---------- Resultado ----------

    [Theory]
    [InlineData("resultado-concluido.json")]
    [InlineData("resultado-erro.json")]
    public void Exemplos_de_resultado_validam_no_schema_e_no_validador(string arquivo)
    {
        var json = Exemplo(arquivo);

        Assert.True(ValidaSchema(SchemaResultado, json));
        var r = Validador.ValidarResultado(Ler<ResultadoAnaliseSofia>(json), SolicitacaoValida());
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
    }

    [Fact]
    public void Resultado_de_erro_criado_pela_fabrica_segue_o_contrato()
    {
        var s = SolicitacaoValida();
        var r = ResultadoAnaliseSofia.Falha(s.IdExecucao!, s.IdSolicitacao!, Opcoes.Agente, "llm.indisponivel", "Serviço indisponível.");

        var json = JsonNode.Parse(JsonSerializer.Serialize(r, JsonContrato.Opcoes))!;

        Assert.True(ValidaSchema(SchemaResultado, json));
        Assert.True(Validador.ValidarResultado(r, s).Sucesso);
        Assert.Equal("erro", json["status"]!.GetValue<string>());
        Assert.Empty(json["achados"]!.AsArray());
    }

    public static TheoryData<string, string> ResultadosInvalidos => new()
    {
        { "outra_execucao", "contrato.correlacao" },
        { "outra_solicitacao", "contrato.correlacao" },
        { "outro_agente", "contrato.agente" },
        { "documento_inexistente", "contrato.referencia" },
        { "status_desconhecido", "contrato.status" },
        { "erro_sem_objeto", "contrato.erro" },
        { "concluido_com_erro", "contrato.erro" },
        { "sem_analise", "contrato.analise" },
        { "sem_achados", "contrato.achados" },
        { "severidade_invalida", "contrato.achado" },
        { "sem_versao", "contrato.versao" },
    };

    [Theory]
    [MemberData(nameof(ResultadosInvalidos))]
    public void Resultado_invalido_e_recusado_pelo_validador(string caso, string codigoEsperado)
    {
        var json = Exemplo("resultado-concluido.json");
        var achado = json["achados"]![0]!;
        switch (caso)
        {
            case "outra_execucao": json["idExecucao"] = Guid.NewGuid().ToString(); break;
            case "outra_solicitacao": json["idSolicitacao"] = Guid.NewGuid().ToString(); break;
            case "outro_agente": json["agente"]!["idAgente"] = "agente-fiscal"; break;
            case "documento_inexistente": achado["idDocumentos"] = new JsonArray("doc-999"); break;
            case "status_desconhecido": json["status"] = "ok"; break;
            case "erro_sem_objeto": json["status"] = "erro"; break;
            case "concluido_com_erro": json["erro"] = new JsonObject { ["codigo"] = "x", ["mensagem"] = "y" }; break;
            case "sem_analise": json["analise"] = " "; break;
            case "sem_achados": json.Remove("achados"); break;
            case "severidade_invalida": achado["severidade"] = "gravissima"; break;
            case "sem_versao": json.Remove("versaoContrato"); break;
        }

        var r = Validador.ValidarResultado(Ler<ResultadoAnaliseSofia>(json), SolicitacaoValida());

        Assert.False(r.Sucesso);
        Assert.Contains(r.Erros, e => e.Codigo == codigoEsperado);
    }

    [Theory]
    [InlineData("status_desconhecido")]
    [InlineData("erro_sem_objeto")]
    [InlineData("concluido_com_erro")]
    [InlineData("sem_achados")]
    [InlineData("sem_agente")]
    [InlineData("severidade_invalida")]
    public void Resultado_invalido_e_recusado_pelo_schema(string caso)
    {
        var json = Exemplo("resultado-concluido.json");
        switch (caso)
        {
            case "status_desconhecido": json["status"] = "ok"; break;
            case "erro_sem_objeto": json["status"] = "erro"; break;
            case "concluido_com_erro": json["erro"] = new JsonObject { ["codigo"] = "x", ["mensagem"] = "y" }; break;
            case "sem_achados": json.Remove("achados"); break;
            case "sem_agente": json.Remove("agente"); break;
            case "severidade_invalida": json["achados"]![0]!["severidade"] = "gravissima"; break;
        }

        Assert.False(ValidaSchema(SchemaResultado, json));
    }

    [Fact]
    public void Resultado_sem_achados_explica_a_ausencia_na_analise()
    {
        var s = SolicitacaoValida();
        var r = new ResultadoAnaliseSofia(ContratoSofia.Versao, s.IdExecucao!, s.IdSolicitacao!, Opcoes.Agente, StatusAnaliseSofia.Concluido,
            "Nenhuma novidade encontrada no período.", [], ["Sem evidência na base coletada."], null);

        Assert.True(Validador.ValidarResultado(r, s).Sucesso);
        Assert.True(ValidaSchema(SchemaResultado, JsonNode.Parse(JsonSerializer.Serialize(r, JsonContrato.Opcoes))!));
    }
}
