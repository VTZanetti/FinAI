namespace FinAI.Api.Services.Budgets;

public sealed record CreateBudgetRequest(Guid CategoryId, int Month, int Year, decimal LimitAmount);

public sealed record UpdateBudgetRequest(decimal LimitAmount);
