using FinAI.Api.Models;

namespace FinAI.Api.DTOs.Budgets;

public sealed record BudgetResponse(
    Guid Id,
    Guid CategoryId,
    int Month,
    int Year,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal ProgressPercent);

public static class BudgetMappings
{
    public static BudgetResponse ToResponse(this Budget budget, decimal spentAmount)
    {
        var progress = budget.LimitAmount > 0
            ? Math.Round(spentAmount / budget.LimitAmount * 100m, 2)
            : 0m;

        return new BudgetResponse(
            budget.Id,
            budget.CategoryId,
            budget.Month,
            budget.Year,
            budget.LimitAmount,
            Math.Round(spentAmount, 2),
            progress);
    }
}
