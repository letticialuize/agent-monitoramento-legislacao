using System.Text;
using Agente.Domain.Comum;

namespace Agente.Application.Comum;

/// <summary>Conversão entre os nomes técnicos dos contratos (snake_case) e os enums do domínio.</summary>
public static class Enumeracao
{
    /// <summary>"ConsultorTributario" → "consultor_tributario"; "AJ" → "AJ" (siglas preservadas).</summary>
    public static string ParaContrato<T>(T valor) where T : struct, Enum
    {
        var nome = valor.ToString();
        if (nome.All(char.IsUpper))
        {
            return nome;
        }

        var sb = new StringBuilder(nome.Length + 4);
        for (var i = 0; i < nome.Length; i++)
        {
            var c = nome[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>Aceita snake_case, PascalCase ou sigla (sem distinção de maiúsculas).</summary>
    public static bool TentarLer<T>(string? texto, out T valor) where T : struct, Enum
    {
        valor = default;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var semSublinhado = texto.Replace("_", string.Empty, StringComparison.Ordinal).Trim();
        return Enum.TryParse(semSublinhado, ignoreCase: true, out valor) && Enum.IsDefined(valor);
    }

    internal static T Ler<T>(string? texto, string campo, string contexto, List<ErroDominio> erros) where T : struct, Enum
    {
        if (TentarLer<T>(texto, out var valor))
        {
            return valor;
        }

        erros.Add(new ErroDominio($"enum.{campo}_invalido", $"[{contexto}] Valor inválido para '{campo}': '{texto}'."));
        return default;
    }
}
