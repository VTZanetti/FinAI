using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.OpenFinance;
using FinAI.Api.Services.OpenFinance.Models;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.OpenFinance;

[Trait("Category", "Unit")]
public class OpenFinanceConnectionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IPluggyClient _pluggy = Substitute.For<IPluggyClient>();
    private readonly IOpenFinanceRepository _repository = Substitute.For<IOpenFinanceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private OpenFinanceConnectionService CreateService() => new(_pluggy, _repository, _unitOfWork);

    [Fact]
    public async Task CreateConnectTokenAsync_UsesUserIdAsClientUserId()
    {
        // Arrange
        _pluggy.CreateConnectTokenAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns(new PluggyConnectTokenResponse("connect-token", 3600));
        var service = CreateService();

        // Act
        var result = await service.CreateConnectTokenAsync(UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("connect-token");
        await _pluggy.Received().CreateConnectTokenAsync(UserId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkConnectionAsync_NewItem_LinksToUser()
    {
        // Arrange
        _repository.GetConnectionAsync(UserId, "item-abc", Arg.Any<CancellationToken>())
            .Returns((FinAI.Api.Models.UserBankConnection?)null);
        var service = CreateService();

        // Act
        var result = await service.LinkConnectionAsync(UserId, "item-abc", "Nubank");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ItemId.Should().Be("item-abc");
        result.Value.InstitutionName.Should().Be("Nubank");
        await _repository.Received().AddConnectionAsync(Arg.Any<FinAI.Api.Models.UserBankConnection>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkConnectionAsync_ExistingItem_ReturnsExisting()
    {
        // Arrange
        var existing = new FinAI.Api.Models.UserBankConnection
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            ItemId = "item-abc",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _repository.GetConnectionAsync(UserId, "item-abc", Arg.Any<CancellationToken>()).Returns(existing);
        var service = CreateService();

        // Act
        var result = await service.LinkConnectionAsync(UserId, "item-abc");

        // Assert: idempotente — não cria duplicata
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(existing.Id);
        await _repository.DidNotReceive().AddConnectionAsync(Arg.Any<FinAI.Api.Models.UserBankConnection>(), Arg.Any<CancellationToken>());
    }
}
