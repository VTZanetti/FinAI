using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.Auth;

[Trait("Category", "Unit")]
public class AuthServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly UserManager<FinAiUser> _userManager = Substitute.For<UserManager<FinAiUser>>(
        Substitute.For<Microsoft.AspNetCore.Identity.IUserStore<FinAiUser>>(), null, null, null, null, null, null, null, null);
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService _refreshTokens = Substitute.For<IRefreshTokenService>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    private AuthService CreateService() => new(
        _userManager,
        _tokenService,
        _refreshTokens,
        _audit,
        Options.Create(new JwtOptions { AccessTokenTtlMinutes = 15 }));

    private void SetupSuccessfulUserManager()
    {
        _userManager.CreateAsync(Arg.Any<FinAiUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<FinAiUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<FinAiUser>())
            .Returns(["User"]);
        _userManager.FindByEmailAsync(Arg.Any<string>())
            .Returns((FinAiUser?)null);
        _tokenService.GenerateAccessToken(Arg.Any<FinAiUser>(), Arg.Any<IReadOnlyList<string>>())
            .Returns("access-token");
        _tokenService.GenerateRefreshToken()
            .Returns("refresh-token");
        _tokenService.HashToken(Arg.Any<string>())
            .Returns("hash");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserWithUserRole()
    {
        // Arrange
        SetupSuccessfulUserManager();
        _userManager.When(u => u.CreateAsync(Arg.Any<FinAiUser>(), "S3nh@Forte!"))
            .Do(callInfo => callInfo.Arg<FinAiUser>().Id = UserId);
        var service = CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterRequest("user@example.com", "S3nh@Forte!", "Ana", "Silva"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(UserId);
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.ExpiresIn.Should().Be(900); // 15 min
        await _userManager.Received().AddToRoleAsync(Arg.Any<FinAiUser>(), "User");
        await _audit.Received().RecordAsync("user.register", "User", UserId, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        _userManager.FindByEmailAsync("user@example.com")
            .Returns(new FinAiUser { Id = Guid.NewGuid(), Email = "user@example.com" });
        var service = CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterRequest("user@example.com", "S3nh@Forte!", "Ana", "Silva"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<FinAiUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_ReturnsValidationError()
    {
        // Arrange
        SetupSuccessfulUserManager();
        _userManager.CreateAsync(Arg.Any<FinAiUser>(), "fraca")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Passwords must have at least 8 characters." }));
        var service = CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterRequest("user@example.com", "fraca", "Ana", "Silva"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new FinAiUser { Id = UserId, Email = "user@example.com" };
        _userManager.FindByEmailAsync("user@example.com").Returns(user);
        _userManager.CheckPasswordAsync(user, "errada").Returns(false);
        var service = CreateService();

        // Act
        var result = await service.LoginAsync(new LoginRequest("user@example.com", "errada"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Unauthorized);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_IssuesTokenPair()
    {
        // Arrange
        SetupSuccessfulUserManager();
        var user = new FinAiUser { Id = UserId, Email = "user@example.com" };
        _userManager.FindByEmailAsync("user@example.com").Returns(user);
        _userManager.CheckPasswordAsync(user, "S3nh@Forte!").Returns(true);
        var service = CreateService();

        // Act
        var result = await service.LoginAsync(new LoginRequest("user@example.com", "S3nh@Forte!"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(UserId);
        await _refreshTokens.Received().IssueAsync(UserId, "refresh-token", Arg.Any<CancellationToken>());
        await _audit.Received().RecordAsync("auth.login", "User", UserId, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_ReturnsUnauthorized()
    {
        // Arrange
        _tokenService.HashToken("token").Returns("hash");
        _refreshTokens.GetActiveByHashAsync("hash", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);
        _refreshTokens.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);
        var service = CreateService();

        // Act
        var result = await service.RefreshAsync("token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndReturnsNewTokens()
    {
        // Arrange
        var stored = new RefreshToken { Id = Guid.NewGuid(), UserId = UserId, TokenHash = "hash" };
        var user = new FinAiUser { Id = UserId, Email = "user@example.com" };
        _tokenService.HashToken("token").Returns("hash");
        _refreshTokens.GetActiveByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(stored);
        _refreshTokens.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(stored);
        _userManager.FindByIdAsync(UserId.ToString()).Returns(user);
        _tokenService.GenerateRefreshToken().Returns("new-refresh");
        _tokenService.GenerateAccessToken(user, Arg.Any<IReadOnlyList<string>>()).Returns("new-access");
        _userManager.GetRolesAsync(user).Returns(["User"]);
        var service = CreateService();

        // Act
        var result = await service.RefreshAsync("token");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-refresh");
        await _refreshTokens.Received().RotateAsync(stored, "new-refresh", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReusedRevokedToken_RevokesFamily()
    {
        // Arrange
        var revoked = new RefreshToken { Id = Guid.NewGuid(), UserId = UserId, TokenHash = "hash", RevokedAt = DateTimeOffset.UtcNow };
        _tokenService.HashToken("old-token").Returns("hash");
        _refreshTokens.GetActiveByHashAsync("hash", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);
        _refreshTokens.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(revoked);
        var service = CreateService();

        // Act
        var result = await service.RefreshAsync("old-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Unauthorized);
        await _refreshTokens.Received().RevokeFamilyAsync(revoked, Arg.Any<CancellationToken>());
        await _audit.Received().RecordAsync("auth.refresh.reuse", "RefreshToken", revoked.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_RevokesActiveToken()
    {
        // Arrange
        var stored = new RefreshToken { Id = Guid.NewGuid(), UserId = UserId, TokenHash = "hash" };
        _tokenService.HashToken("token").Returns("hash");
        _refreshTokens.GetActiveByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(stored);
        var service = CreateService();

        // Act
        var result = await service.LogoutAsync("token");

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _refreshTokens.Received().RevokeAsync(stored, Arg.Any<CancellationToken>());
        await _audit.Received().RecordAsync("auth.logout", "User", UserId, null, Arg.Any<CancellationToken>());
    }
}
