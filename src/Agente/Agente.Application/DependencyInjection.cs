using Agente.Application.Catalogo;
using Agente.Application.Ferramentas;
using Agente.Application.Llm;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Microsoft.Extensions.DependencyInjection;

namespace Agente.Application;

public static class DependencyInjection
{
    /// <summary>Casos de uso e serviços da camada de aplicação (sem dependências de infraestrutura).</summary>
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddScoped<RegistradorEtapas>();
        services.AddScoped<ValidadorSaidaLlm>();

        services.AddScoped<ImportarCatalogo>();
        services.AddScoped<AtivarVersaoCatalogo>();

        services.AddScoped<ColetarFonte>();
        services.AddScoped<ClassificarNovidadesComLlm>();
        services.AddScoped<ExecutarCicloColeta>();
        services.AddScoped<ConsultarNovidades>();
        services.AddScoped<FerramentasNovidades>();
        services.AddScoped<ResponderMensagemConversa>();
        services.AddScoped<Sofia.AnalisarSolicitacaoSofia>();

        services.AddScoped<AplicarRetencao>();
        return services;
    }
}
