using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Categories;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Categories;

[Trait("Category", "Unit")]
public class CategoryServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CategoryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CategoryService CreateService() => new(_categories, _unitOfWork);

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        _categories.ExistsByNameAsync(UserId, "Food", "Restaurant", Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();

        // Act
        var result = await service.CreateAsync(UserId, new CreateCategoryRequest("Food", "Restaurant"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_SystemCategory_ReturnsForbidden()
    {
        // Arrange
        var system = new Category { Id = CategoryId, UserId = Guid.Empty, Name = "Food", IsSystem = true };
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(system);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(UserId, CategoryId, new UpdateCategoryRequest("Comida"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Forbidden); // 403 — categorias do sistema são somente leitura
    }

    [Fact]
    public async Task DeleteAsync_CategoryWithTransactions_ReturnsConflict()
    {
        // Arrange
        var category = new Category { Id = CategoryId, UserId = UserId, Name = "Food" };
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(category);
        _categories.CountTransactionsAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(5);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, CategoryId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict);
        _categories.DidNotReceive().Delete(category);
    }

    [Fact]
    public async Task DeleteAsync_ValidCategory_Deletes()
    {
        // Arrange
        var category = new Category { Id = CategoryId, UserId = UserId, Name = "Food" };
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(category);
        _categories.CountTransactionsAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns(0);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, CategoryId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _categories.Received().Delete(category);
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
