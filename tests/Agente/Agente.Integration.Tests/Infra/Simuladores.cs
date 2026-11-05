using System.Text.Json;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Agente.Integration.Tests.Infra;

/// <summary>
/// Respostas das fontes no formato documentado (research R-03) e da API de chat da OpenAI.
/// Os itens têm datas dentro da janela padrão (relógio fixo em 2026-10-29).
/// </summary>
public static class Simuladores
{
    public const string EmentaCamara = "Regulamenta a Contribuição sobre Bens e Serviços no comércio varejista.";
    public const string EmentaSenado = "Altera a Lei nº 8.171 para dispor sobre crédito rural.";
    public const string EmentaPagina = "Portaria nº 123 - Dispõe sobre a CBS nas notas de serviço";

    public static string UrlCamara(WireMockServer s) => $"{s.Url}/camara/api/v2/proposicoes/9001";

    public static string UrlSenado(WireMockServer s) => $"{s.Url}/senado/normas/lei-15526";

    public static void Fontes(WireMockServer s, string? ementaCamara = null)
    {
        // Senado: lista vazia por padrão; norma no dia 27/10 e detalhe com publicação em 28/10.
        s.Given(Request.Create().WithPath("/senado/dadosabertos/legislacao/lista.json").UsingGet())
            .AtPriority(10)
            .RespondWith(Json(new { ListaDocumento = new { documentos = new { documento = Array.Empty<object>() } } }));
        s.Given(Request.Create().WithPath("/senado/dadosabertos/legislacao/lista.json").WithParam("data", "20261027").UsingGet())
            .AtPriority(1)
            .RespondWith(Json(new
            {
                ListaDocumento = new
                {
                    documentos = new
                    {
                        documento = new[] { new { id = "44095919", tipo = "LEI-n", descricao = "Lei Numerada", numero = "15526", ementa = EmentaSenado, dataassinatura = "27/10/2026" } },
                    },
                },
            }));
        s.Given(Request.Create().WithPath("/senado/dadosabertos/legislacao/44095919.json").UsingGet())
            .RespondWith(Json(new
            {
                DetalheDocumento = new
                {
                    documento = new
                    {
                        identificacao = new { urlDocumento = UrlSenado(s) },
                        publicacoes = new { publicacao = new[] { new { fonte = "Diário Oficial da União de 28/10/2026", data = "28/10/2026" } } },
                    },
                },
            }));

        // Câmara: uma proposição apresentada em 27/10.
        s.Given(Request.Create().WithPath("/camara/api/v2/proposicoes").UsingGet())
            .RespondWith(Json(new
            {
                dados = new[]
                {
                    new { id = 9001, uri = UrlCamara(s), siglaTipo = "PLP", numero = 68, ano = 2026, ementa = ementaCamara ?? EmentaCamara, dataApresentacao = "2026-10-27T10:23" },
                },
                links = new[] { new { rel = "self", href = "x" } },
            }));

        // Fonte fora do ar.
        s.Given(Request.Create().WithPath(new WildcardMatcher("/falha/*")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        // Querido Diário (agregador): diário municipal de 28/10.
        s.Given(Request.Create().WithPath("/qd/gazettes").UsingGet())
            .RespondWith(Json(new
            {
                total_gazettes = 1,
                gazettes = new[]
                {
                    new
                    {
                        territory_id = "3550308", territory_name = "São Paulo", state_code = "SP", date = "2026-10-28", edition = "200",
                        is_extra_edition = false, url = "https://data.queridodiario.ok.org.br/3550308/2026-10-28/x.pdf",
                        excerpts = new[] { "Altera a alíquota do ISS para serviços de saúde." },
                    },
                },
            }));

        // Página oficial cujo item aponta para site fora do catálogo.
        s.Given(Request.Create().WithPath("/pagina/").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "text/html; charset=utf-8").WithBody(
                $"""
                <html><body><ul>
                  <li class="ato"><span class="titulo">{EmentaPagina}</span> <span class="data">28/10/2026</span> <a href="https://blog.exemplo.com/portaria-123">ver</a></li>
                  <li class="ato"><span class="titulo">Ato antigo</span> <span class="data">01/01/2020</span> <a href="/pagina/antigo">ver</a></li>
                </ul></body></html>
                """));
    }

    public static IResponseBuilder Json(object corpo) =>
        Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(JsonSerializer.Serialize(corpo));

    // ---------- OpenAI (chat completions) ----------

    public static string Completion(string? conteudo, object? toolCalls = null, bool comUso = true)
    {
        var mensagem = new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = conteudo };
        if (toolCalls is not null)
        {
            mensagem["tool_calls"] = toolCalls;
        }

        var corpo = new Dictionary<string, object?>
        {
            ["id"] = "chatcmpl-teste",
            ["object"] = "chat.completion",
            ["created"] = 1_790_000_000,
            ["model"] = "modelo-teste",
            ["choices"] = new[] { new Dictionary<string, object?> { ["index"] = 0, ["message"] = mensagem, ["finish_reason"] = toolCalls is null ? "stop" : "tool_calls" } },
        };
        if (comUso)
        {
            corpo["usage"] = new { prompt_tokens = 120, completion_tokens = 30, total_tokens = 150 };
        }

        return JsonSerializer.Serialize(corpo);
    }

    public static object ChamadaFerramenta(string nome, object argumentos) => new[]
    {
        new { id = "call_1", type = "function", function = new { name = nome, arguments = JsonSerializer.Serialize(argumentos) } },
    };

    /// <summary>Saída estruturada (classificação): resposta sem uso informado para o cenário 13.</summary>
    public static void ClassificacaoSemUso(WireMockServer openAi) =>
        openAi.Given(Request.Create().WithPath("/v1/chat/completions").WithBody(new WildcardMatcher("*json_schema*")).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json")
                .WithBody(Completion("""{"classificacoes":[]}""", comUso: false)));

    /// <summary>Conversa: 1ª chamada pede a ferramenta; após o retorno da ferramenta, responde com <paramref name="respostaFinal"/>.</summary>
    public static void Conversa(WireMockServer openAi, string nomeFerramenta, object argumentos, string respostaFinal)
    {
        openAi.Given(Request.Create().WithPath("/v1/chat/completions").WithBody(new WildcardMatcher("*\"role\":\"tool\"*")).UsingPost())
            .AtPriority(2)
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody(Completion(respostaFinal)));
        openAi.Given(Request.Create().WithPath("/v1/chat/completions").UsingPost())
            .AtPriority(5)
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json")
                .WithBody(Completion(null, ChamadaFerramenta(nomeFerramenta, argumentos))));
    }
}
