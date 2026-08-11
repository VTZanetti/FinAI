using System.Text;

namespace FinAI.Api.Services.AI;

/// <summary>
/// Normalização de texto para classificação: uppercase + remoção de acentos.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Trim().ToUpperInvariant();
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var mapped = c switch
            {
                'Á' or 'À' or 'Â' or 'Ã' or 'Ä' => 'A',
                'É' or 'È' or 'Ê' or 'Ë' => 'E',
                'Í' or 'Ì' or 'Î' or 'Ï' => 'I',
                'Ó' or 'Ò' or 'Ô' or 'Õ' or 'Ö' => 'O',
                'Ú' or 'Ù' or 'Û' or 'Ü' => 'U',
                'Ç' => 'C',
                _ => c
            };
            sb.Append(mapped);
        }

        return sb.ToString();
    }
}
