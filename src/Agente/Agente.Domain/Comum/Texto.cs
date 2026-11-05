using System.Globalization;
using System.Text;

namespace Agente.Domain.Comum;

/// <summary>Normalização de texto usada em comparações determinísticas.</summary>
public static class Texto
{
    /// <summary>Minúsculas, sem acentos e com espaços simples.</summary>
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var decomposto = valor.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var espacoAnterior = false;
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!espacoAnterior && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                espacoAnterior = true;
                continue;
            }

            sb.Append(char.ToLowerInvariant(c));
            espacoAnterior = false;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Verifica se <paramref name="termo"/> aparece como palavra ou expressão em <paramref name="texto"/>.</summary>
    public static bool ContemTermo(string textoNormalizado, string termo)
    {
        var t = Normalizar(termo);
        if (t.Length == 0)
        {
            return false;
        }

        var indice = 0;
        while ((indice = textoNormalizado.IndexOf(t, indice, StringComparison.Ordinal)) >= 0)
        {
            var inicioOk = indice == 0 || !char.IsLetterOrDigit(textoNormalizado[indice - 1]);
            var fim = indice + t.Length;
            var fimOk = fim == textoNormalizado.Length || !char.IsLetterOrDigit(textoNormalizado[fim]);
            if (inicioOk && fimOk)
            {
                return true;
            }

            indice = fim;
        }

        return false;
    }
}
