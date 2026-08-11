using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Budgets;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Budgets;

[Trait("Category", "Unit")]
public class BudgetServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CategoryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    private BudgetService CreateService() => new(_budgets, _categories, _transactions, _unitOfWork, _audit);

    [Fact]
    public async Task CreateAsync_InvalidMonth_ReturnsValidationError()
    {
        // Arrange
        var service = CreateService();
        var request = new CreateBudgetRequest(CategoryId, 13, 2026, 800m);

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCategoryPeriod_ReturnsConflict()
    {
        // Arrange
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(new Category { Id = CategoryId, Name = "Food" });
        _budgets.ExistsByCategoryPeriodAsync(UserId, CategoryId, 2026, 8, Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();
        var request = new CreateBudgetRequest(CategoryId, 8, 2026, 800m);

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_Succeeds()
    {
        // Arrange
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(new Category { Id = CategoryId, Name = "Food" });
        _budgets.ExistsByCategoryPeriodAsync(UserId, CategoryId, 2026, 8, Arg.Any<CancellationToken>()).Returns(false);
        var service = CreateService();
        var request = new CreateBudgetRequest(CategoryId, 8, 2026, 800m);

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Month.Should().Be(8);
        result.Value.Year.Should().Be(2026);
        result.Value.LimitAmount.Should().Be(800m);
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSpentAmountAsync_ReturnsAbsoluteExpenses()
    {
        // Arrange
        _transactions.SumExpensesByCategoryAsync(UserId, CategoryId, 2026, 8, Arg.Any<CancellationToken>())
            .Returns(-342.10m); // soma de despesas (negativa no banco)
        var service = CreateService();
        var budget = new Budget { Id = Guid.NewGuid(), UserId = UserId, CategoryId = CategoryId, Month = 8, Year = 2026, LimitAmount = 800m };

        // Act
        var spent = await service.GetSpentAmountAsync(UserId, budget);

        // Assert
        spent.Should().Be(342.10m); // gasto absoluto
    }

    [Fact]
    public async Task UpdateAsync_BudgetNotOwned_ReturnsNotFound()
    {
        // Arrange
        _budgets.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>()).Returns((Budget?)null);
        var service = CreateService();
        var request = new UpdateBudgetRequest(900m);

        // Act
        var result = await service.UpdateAsync(UserId, Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ValidBudget_Deletes()
    {
        // Arrange
        var budget = new Budget { Id = Guid.NewGuid(), UserId = UserId, CategoryId = CategoryId, Month = 8, Year = 2026 };
        _budgets.GetByIdAsync(budget.Id, UserId, Arg.Any<CancellationToken>()).Returns(budget);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, budget.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _budgets.Received().Delete(budget);
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
