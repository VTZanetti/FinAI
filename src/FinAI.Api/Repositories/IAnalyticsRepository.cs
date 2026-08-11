using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.Repositories;

/// <summary>
/// Consultas agregadas de analytics — sempre no banco (GROUP BY), nunca em memória (NFR-02).
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>Soma receitas/despesas no período (valores em módulo para despesas).</summary>
    Task<TotalsResult> GetTotalsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default);

    /// <summary>Despesas por categoria no período (com categorias "Uncategorized").</summary>
    Task<IReadOnlyList<CategoryAggregate>> GetExpensesByCategoryAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default);

    /// <summary>Soma das despesas recorrentes no período.</summary>
    Task<decimal> GetRecurringExpensesAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default);

    /// <summary>Totais mensais (receitas/despesas por mês) para a série.</summary>
    Task<IReadOnlyList<MonthlyAggregate>> GetMonthlyTotalsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default);
}

public sealed record TotalsResult(decimal Income, decimal Expenses, decimal Balance);

public sealed record CategoryAggregate(string? CategoryName, string? Subcategory, decimal Amount)
{
    public string EffectiveCategory => CategoryName ?? "Uncategorized";
}

public sealed record MonthlyAggregate(int Year, int Month, decimal Income, decimal Expenses);
