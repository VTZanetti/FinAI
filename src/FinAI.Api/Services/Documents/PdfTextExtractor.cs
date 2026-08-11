using UglyToad.PdfPig;

namespace FinAI.Api.Services.Documents;

public interface ITextExtractor
{
    Task<string> ExtractAsync(Stream stream, string contentType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extração de texto: PDFs via PdfPig; texto puro para text/plain.
/// </summary>
public class PdfTextExtractor : ITextExtractor
{
    public Task<string> ExtractAsync(Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        return contentType switch
        {
            "text/plain" => ExtractPlainTextAsync(stream, cancellationToken),
            "application/pdf" => Task.FromResult(ExtractPdf(stream)),
            _ => throw new NotSupportedException($"Content type not supported: {contentType}")
        };
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string ExtractPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var text = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            text.AppendLine(page.Text);
        }

        return text.ToString();
    }
}
