using Agente.Application;
using Agente.Application.Comum;
using Agente.Infrastructure;
using Api.Agente.Agendamento;
using Api.Agente.Endpoints;
using Api.Agente.Inicializacao;
using Api.Agente.Observabilidade;

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

// Só em Development: a API está sem login (ADR 0007) e esta tela não deve ser exposta.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Api.Agente v1"));
}


app.MapGet("/saude", () => Results.Ok(new { status = "ok" }));
app.MapConsultas();
app.MapTarefas();

await app.RunAsync();

/// <summary>Ponto de entrada (exposto para os testes de integração).</summary>
public partial class Program;
