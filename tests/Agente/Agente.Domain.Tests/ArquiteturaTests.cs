using System.Reflection;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Infrastructure.Persistencia;

namespace Agente.Domain.Tests;

/// <summary>Regra de dependência da Clean Architecture e invariantes DDD (plan.md "Arquitetura", ADR 0006).</summary>
public class ArquiteturaTests
{
    private static readonly Assembly Dominio = typeof(RaizAgregado).Assembly;
    private static readonly Assembly Aplicacao = typeof(IUnidadeDeTrabalho).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static readonly string[] ProibidosNoDominio =
        ["Microsoft.EntityFrameworkCore", "Microsoft.SemanticKernel", "MongoDB", "Microsoft.AspNetCore", "MailKit", "AngleSharp", "UglyToad.PdfPig", "JsonSchema.Net", "Npgsql", "Agente.Application", "Agente.Infrastructure"];

    private static readonly string[] ProibidosNaAplicacao =
        ["Microsoft.EntityFrameworkCore", "Microsoft.SemanticKernel", "MongoDB", "Microsoft.AspNetCore", "MailKit", "AngleSharp", "UglyToad.PdfPig", "Npgsql", "OpenAI", "Agente.Infrastructure"];

    [Fact]
    public void Dominio_nao_referencia_bibliotecas_externas()
    {
        var violacoes = Dominio.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => ProibidosNoDominio.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void Aplicacao_nao_referencia_infraestrutura()
    {
        var violacoes = Aplicacao.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => ProibidosNaAplicacao.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void Agregados_e_entidades_nao_tem_setter_publico()
    {
        var violacoes = Dominio.GetTypes()
            .Where(t => typeof(Entidade).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void Endpoints_e_agendadores_nao_usam_DbContext()
    {
        var alvo = typeof(AgenteDbContext);
        var violacoes = Api.GetTypes()
            .Where(t => t.Namespace is "Api.Agente.Endpoints" or "Api.Agente.Agendamento")
            .Where(t => UsaTipo(t, alvo))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violacoes);
    }

    private static bool UsaTipo(Type t, Type alvo)
    {
        const BindingFlags todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return t.GetFields(todos).Any(f => f.FieldType == alvo)
            || t.GetConstructors(todos).Any(c => c.GetParameters().Any(p => p.ParameterType == alvo))
            || t.GetMethods(todos).Any(m => m.GetParameters().Any(p => p.ParameterType == alvo) || m.ReturnType == alvo);
    }
}
