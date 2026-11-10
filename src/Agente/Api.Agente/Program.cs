using Agente.Application;
using Agente.Application.Comum;
using Agente.Infrastructure;
using Api.Agente.A2A;
using Api.Agente.Agendamento;
using Api.Agente.Endpoints;
using Api.Agente.Inicializacao;
using Api.Agente.Observabilidade;
using Api.Agente.Seguranca;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservabilidadeAgente();

builder.Services.Configure<OpcoesConsulta>(builder.Configuration.GetSection("Consulta"));
builder.Services.Configure<OpcoesColeta>(builder.Configuration.GetSection("Coleta"));
builder.Services.Configure<OpcoesFerramentas>(builder.Configuration.GetSection("Ferramentas"));
builder.Services.Configure<OpcoesMemoria>(builder.Configuration.GetSection("Memoria"));
builder.Services.Configure<OpcoesValidacaoLlm>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<OpcoesRetencao>(builder.Configuration.GetSection("Retencao"));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    JsonContrato.Configurar(o.SerializerOptions);
    // Contratos com additionalProperties: false (ex.: ConsultaRequest, Princípio VII).
    o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
// Em Development o padrão é lançar exceção em requisição inválida, o que viraria 500 em vez de 400.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(o => o.AddSchemaTransformer((schema, contexto, _) =>
{
    if (contexto.JsonTypeInfo.Type == typeof(ConsultaRequest))
    {
        schema.Examples = [ConsultasEndpoints.ExemploConsulta(DateOnly.FromDateTime(DateTime.Today))];
    }
    else if (contexto.JsonTypeInfo.Type == typeof(MensagemRequest))
    {
        schema.Examples = [ConsultasEndpoints.ExemploMensagem()];
    }

    return Task.CompletedTask;
}));
// CORS só para o frontend da SOFIA no navegador; a SOFIA backend não depende dele (feature 003).
builder.Services.AddCorsSofia(builder.Configuration);
// Contrato SOFIA v1.0 e Agent Card A2A v0.3 (feature 004).
builder.Services.AddContratoSofiaA2A(builder.Configuration);
builder.Services.AddAplicacao();
builder.Services.AddInfraestrutura(builder.Configuration);

if (builder.Configuration.GetValue("Agendamento:Habilitado", true))
{
    builder.Services.AddHostedService<AgendadorColeta>();
    builder.Services.AddHostedService<AgendadorRetencao>();
}

var app = builder.Build();

await Agente.Infrastructure.DependencyInjection.PrepararBancoAsync(app.Services, CancellationToken.None);
await CargaCatalogoInicial.ExecutarAsync(app.Services, CancellationToken.None);

app.UseExceptionHandler();
app.UseMiddleware<MiddlewareCorrelacao>();
// Sem política padrão: só as rotas marcadas com RequireCorsSofia recebem cabeçalhos CORS.
app.UseCors();

// Sempre em Development. Fora dele, só com Swagger:Habilitado=true: a API está sem login (ADR 0007), então
// a tela fica ligada apenas enquanto a responsável quiser, para a integração com o ambiente publicado.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Habilitado", false))
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Api.Agente v1"));
    // A raiz abre o Swagger. Com o Swagger desligado, a raiz continua respondendo 404.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}


app.MapGet("/saude", () => Results.Ok(new { status = "ok" }));
app.MapConsultas();
app.MapTarefas();
app.MapAgentCard();
app.MapA2A();

await app.RunAsync();

/// <summary>Ponto de entrada (exposto para os testes de integração).</summary>
public partial class Program;
