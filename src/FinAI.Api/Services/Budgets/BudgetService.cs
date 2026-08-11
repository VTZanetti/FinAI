using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;

namespace FinAI.Api.Services.Budgets;

public class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _budgets;
    private readonly ICategoryRepository _categories;
    private readonly ITransactionRepository _transactions;
    private readonly IUnitOfWork _unitOfWork;

    public BudgetService(
        IBudgetRepository budgets,
        ICategoryRepository categories,
        ITransactionRepository transactions,
        IUnitOfWork unitOfWork)
    {
        _budgets = budgets;
        _categories = categories;
        _transactions = transactions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Budget>> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Month is < 1 or > 12)
            return Result.Failure<Budget>(ErrorCode.Validation, "Month must be between 1 and 12");

        // Categoria deve pertencer ao usuário (ou ser do sistema)
        if (await _categories.GetByIdAsync(request.CategoryId, userId, cancellationToken) is null)
            return Result.Failure<Budget>(ErrorCode.NotFound, "Category not found");

        // Um orçamento por (usuário, categoria, mês, ano)
        if (await _budgets.ExistsByCategoryPeriodAsync(userId, request.CategoryId, request.Year, request.Month, cancellationToken))
            return Result.Failure<Budget>(ErrorCode.Conflict, "Budget already exists for this category and period");

        var budget = new Budget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = request.CategoryId,
            Month = request.Month,
            Year = request.Year,
            LimitAmount = request.LimitAmount,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _budgets.AddAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(budget);
    }

    public async Task<Result<Budget>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(id, userId, cancellationToken);
        return budget is null
            ? Result.Failure<Budget>(ErrorCode.NotFound, "Budget not found")
            : Result.Success(budget);
    }

    public async Task<Result<IReadOnlyList<Budget>>> ListAsync(Guid userId, int? month, int? year, CancellationToken cancellationToken = default)
    {
        var budgets = await _budgets.ListByPeriodAsync(userId, month, year, cancellationToken);
        return Result.Success(budgets);
    }

    public async Task<Result<Budget>> UpdateAsync(Guid userId, Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(id, userId, cancellationToken);
        if (budget is null)
            return Result.Failure<Budget>(ErrorCode.NotFound, "Budget not found");

        budget.LimitAmount = request.LimitAmount;
        budget.UpdatedAt = DateTimeOffset.UtcNow;

        _budgets.Update(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(budget);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(id, userId, cancellationToken);
        if (budget is null)
            return Result.Failure(ErrorCode.NotFound, "Budget not found");

        _budgets.Delete(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Calcula o valor gasto (despesas) da categoria no período do orçamento.
    /// </summary>
    public async Task<decimal> GetSpentAmountAsync(Guid userId, Budget budget, CancellationToken cancellationToken = default)
        => Math.Abs(await _transactions.SumExpensesByCategoryAsync(userId, budget.CategoryId, budget.Year, budget.Month, cancellationToken));
}
