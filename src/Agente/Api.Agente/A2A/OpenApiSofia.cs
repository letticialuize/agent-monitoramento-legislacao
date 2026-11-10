using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Agente.A2A;

/// <summary>Marca as rotas protegidas pela chave da SOFIA, para o OpenAPI declarar o Bearer só nelas.</summary>
public sealed class ExigeTokenSofia;

/// <summary>
/// OpenAPI das rotas A2A: esquema Bearer (botão Authorize do Swagger) e exemplo pronto de
/// <c>message/send</c>. O valor da chave nunca entra no documento.
/// </summary>
public static class OpenApiSofia
{
    public const string Esquema = "sofiaBearer";

    public static OpenApiOptions AddSofiaA2A(this OpenApiOptions o)
    {
        o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Components ??= new OpenApiComponents();
            doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            doc.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "Chave compartilhada da SOFIA (SOFIA_A2A_TOKEN). Cole só a chave, sem a palavra Bearer.",
            };
            return Task.CompletedTask;
        });

        o.AddOperationTransformer((op, ctx, _) =>
        {
            if (ctx.Description.ActionDescriptor.EndpointMetadata.OfType<ExigeTokenSofia>().Any())
            {
                op.Security ??= [];
                op.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Esquema, ctx.Document)] = [] });
            }

            return Task.CompletedTask;
        });

        o.AddSchemaTransformer((schema, ctx, _) =>
        {
            if (ctx.JsonTypeInfo.Type == typeof(RequisicaoJsonRpc))
            {
                schema.Examples = [ExemploMessageSend()];
            }

            return Task.CompletedTask;
        });

        return o;
    }

    /// <summary>Exemplo válido com ids novos a cada geração do documento (dados sintéticos).</summary>
    public static JsonObject ExemploMessageSend()
    {
        var solicitacao = new JsonObject
        {
            ["versaoContrato"] = "1.0",
            ["idExecucao"] = Guid.NewGuid().ToString(),
            ["idSolicitacao"] = Guid.NewGuid().ToString(),
            ["usuario"] = new JsonObject { ["nome"] = "Usuário de Teste" },
            ["pergunta"] = "Quais as novidades de ICMS nos últimos 7 dias?",
            ["documentos"] = new JsonArray(new JsonObject
            {
                ["idDocumento"] = "doc-001",
                ["nome"] = "nota-teste.txt",
                ["tipo"] = "text/plain",
                ["texto"] = "Documento sintético de teste: venda interestadual de mercadoria com ICMS.",
            }),
        };

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "rpc-swagger-1",
            ["method"] = ProtocoloA2A.MetodoEnviarMensagem,
            ["params"] = new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "user",
                    ["messageId"] = Guid.NewGuid().ToString(),
                    ["kind"] = "message",
                    ["parts"] = new JsonArray(new JsonObject { ["kind"] = "text", ["text"] = solicitacao.ToJsonString() }),
                },
            },
        };
    }
}
