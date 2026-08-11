using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.Services.AI;

/// <summary>
/// Resultado de classificação — sempre carrega a fonte (rules | cached | llm | fallback).
/// </summary>
public sealed record ClassificationResult(
    Guid? CategoryId,
    string Category,
    string? Subcategory,
    decimal Confidence,
    string Source);

public interface IRuleClassifier
{
    ClassificationResult? Match(string description);
}

/// <summary>
/// Classificação por keywords (sem LLM) — ADR-005, etapa 1 da cascata.
/// </summary>
public class RuleClassifier : IRuleClassifier
{
    private const decimal RuleConfidence = 0.85m;

    // Regras: keywords (normalizadas) → categoria/subcategoria do sistema
    private static readonly IReadOnlyList<(string[] Keywords, string Category, string? Subcategory)> Rules =
    [
        (["UBER", "TAXI", "99APP", "99 POP", "CABIFY"], "Transportation", "Ride Sharing"),
        (["IFOOD", "RESTAURANTE", "RESTAURANT", "MCDONALD", "BURGER KING", "OUTBACK", "HABIB"], "Food", "Restaurant"),
        (["MERCADO", "SUPERMERCADO", "EXTRA", "PÃO DE AÇÚCAR", "PAO DE ACUCAR", "CARREFOUR", "ASSAI", "ATACADAO"], "Food", "Groceries"),
        (["ENERGIA", "ENEL", "CPFL", "LUZ", "ELETRICIDADE"], "Utilities", "Electricity"),
        (["AGUA", "SABESP", "COPASA", "CEDAE"], "Utilities", "Water"),
        (["INTERNET", "FIBRA", "VIVO", "CLARO", "TIM", "OI FIBRA"], "Utilities", "Internet"),
        (["FARMACIA", "DROGARIA", "DROGA RAIA", "PAGUE MENOS", "PANVEL"], "Health", "Pharmacy"),
        (["CONSULTA", "MEDICO", "DENTISTA", "CLINICA", "HOSPITAL"], "Health", "Medical"),
        (["NETFLIX", "SPOTIFY", "PRIME VIDEO", "DISNEY", "HBO", "STREAMING", "YOUTUBE PREMIUM"], "Entertainment", "Streaming"),
        (["STEAM", "PLAYSTATION", "XBOX", "NINTENDO", "GAMES"], "Entertainment", "Games"),
        (["CURSO", "FACULDADE", "UNIVERSIDADE", "COURSE", "ALURA", "UDEMY"], "Education", "Courses"),
        (["AEREA", "VOO", "COMPANHIA AEREA", "AZUL", "GOL", "LATAM"], "Travel", "Flights"),
        (["AMAZON", "MERCADO LIVRE", "SHOPEE", "ALIEXPRESS", "EBAY"], "Shopping", "Online"),
        (["SALARIO", "SALARY", "PAGAMENTO DE SALARIO", "HOLERITE"], "Income", "Salary"),
        (["FREELA", "FREELANCE", "HONORARIOS", "PJ"], "Income", "Freelance"),
        (["ALUGUEL", "RENT", "IPTU", "CONDOMINIO"], "Housing", "Rent")
    ];

    public ClassificationResult? Match(string description)
    {
        var normalized = TextNormalizer.Normalize(description);
        if (normalized.Length == 0)
            return null;

        foreach (var (keywords, category, subcategory) in Rules)
        {
            foreach (var keyword in keywords)
            {
                var normalizedKeyword = TextNormalizer.Normalize(keyword);
                if (normalized.Contains(normalizedKeyword, StringComparison.Ordinal))
                {
                    return new ClassificationResult(null, category, subcategory, RuleConfidence, "rules");
                }
            }
        }

        return null;
    }
}
