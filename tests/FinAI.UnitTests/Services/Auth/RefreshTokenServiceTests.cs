using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.Auth;

[Trait("Category", "Unit")]
public class RefreshTokenServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RefreshTokenService CreateService() => new(
        _tokens,
        _tokenService,
        _unitOfWork,
        Options.Create(new JwtOptions { RefreshTokenTtlDays = 30 }));

    private static RefreshToken Token(DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null, Guid? replacedById = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            TokenHash = "hash",
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
            RevokedAt = revokedAt,
            ReplacedById = replacedById
        };

    [Fact]
    public async Task IssueAsync_CreatesActiveTokenWithHash()
    {
        // Arrange
        var service = CreateService();
        _tokenService.HashToken("raw-token").Returns("hash-value");

        // Act
        var token = await service.IssueAsync(UserId, "raw-token");

        // Assert
        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash-value");
        token.RevokedAt.Should().BeNull();
        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
        await _tokens.Received().AddAsync(token, Arg.Any<CancellationToken>());
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActiveByHash_RevokedToken_ReturnsNull()
    {
        // Arrange
        var revoked = Token(revokedAt: DateTimeOffset.UtcNow);
        _tokens.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(revoked);
        var service = CreateService();

        // Act
        var result = await service.GetActiveByHashAsync("hash");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByHash_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var expired = Token(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        _tokens.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(expired);
        var service = CreateService();

        // Act
        var result = await service.GetActiveByHashAsync("hash");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RotateAsync_RevokesCurrent_AndCreatesReplacement()
    {
        // Arrange
        var current = Token();
        _tokenService.HashToken("new-token").Returns("new-hash");
        var service = CreateService();

        // Act
        await service.RotateAsync(current, "new-token");

        // Assert
        current.RevokedAt.Should().NotBeNull();
        current.ReplacedByTokenId.Should().NotBeNull();
        _tokens.Received().Update(current);
        await _tokens.Received().AddAsync(Arg.Is<RefreshToken>(t => t.TokenHash == "new-hash" && t.ReplacedById == current.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeFamilyAsync_RevokesTokenAndChain()
    {
        // Arrange
        var token = Token();
        var descendant = Token();
        _tokens.GetChainAsync(token, Arg.Any<CancellationToken>()).Returns([descendant]);
        var service = CreateService();

        // Act
        await service.RevokeFamilyAsync(token);

        // Assert
        token.RevokedAt.Should().NotBeNull();
        descendant.RevokedAt.Should().NotBeNull();
        _tokens.Received(2).Update(Arg.Any<RefreshToken>());
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
