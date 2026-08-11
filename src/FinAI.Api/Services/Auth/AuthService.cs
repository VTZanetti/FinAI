using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Services.Audit;
using Microsoft.AspNetCore.Identity;

namespace FinAI.Api.Services.Auth;

public interface IAuthService
{
    Task<Result<AuthResult>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    public const string RoleUser = "User";
    public const string RoleAdmin = "Admin";

    private readonly UserManager<FinAiUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IAuditService _audit;
    private readonly JwtOptions _options;

    public AuthService(
        UserManager<FinAiUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokens,
        IAuditService audit,
        Microsoft.Extensions.Options.IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<Result<AuthResult>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Result.Failure<AuthResult>(ErrorCode.Conflict, "A user with this email already exists");

        var user = new FinAiUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure<AuthResult>(ErrorCode.Validation, errors);
        }

        await _userManager.AddToRoleAsync(user, RoleUser);

        var authResult = await IssueTokenPairAsync(user, cancellationToken);

        await _audit.RecordAsync("user.register", "User", user.Id,
            new { email = MaskEmail(user.Email ?? string.Empty) }, cancellationToken);

        return Result.Success(authResult);
    }

    public async Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _audit.RecordAsync("auth.login", "User", user?.Id, null, cancellationToken);
            return Result.Failure<AuthResult>(ErrorCode.Unauthorized, "Invalid email or password");
        }

        var authResult = await IssueTokenPairAsync(user, cancellationToken);

        await _audit.RecordAsync("auth.login", "User", user.Id,
            new { email = MaskEmail(user.Email ?? string.Empty) }, cancellationToken);

        return Result.Success(authResult);
    }

    public async Task<Result<AuthResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var stored = await _refreshTokens.GetActiveByHashAsync(tokenHash, cancellationToken);

        if (stored is null)
        {
            // Reuso de token revogado/expirado → possível roubo: revoga a família
            var revoked = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken);
            if (revoked is not null)
            {
                await _refreshTokens.RevokeFamilyAsync(revoked, cancellationToken);
                await _audit.RecordAsync("auth.refresh.reuse", "RefreshToken", revoked.Id, null, cancellationToken);
            }

            return Result.Failure<AuthResult>(ErrorCode.Unauthorized, "Invalid refresh token");
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return Result.Failure<AuthResult>(ErrorCode.Unauthorized, "Invalid refresh token");

        var newToken = _tokenService.GenerateRefreshToken();
        await _refreshTokens.RotateAsync(stored, newToken, cancellationToken);

        var authResult = await BuildAuthResultAsync(user, newToken, cancellationToken);

        await _audit.RecordAsync("auth.refresh", "User", user.Id, null, cancellationToken);

        return Result.Success(authResult);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var stored = await _refreshTokens.GetActiveByHashAsync(tokenHash, cancellationToken);

        if (stored is null)
            return Result.Success(); // idempotente

        await _refreshTokens.RevokeAsync(stored, cancellationToken);
        await _audit.RecordAsync("auth.logout", "User", stored.UserId, null, cancellationToken);

        return Result.Success();
    }

    private async Task<AuthResult> IssueTokenPairAsync(FinAiUser user, CancellationToken cancellationToken)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        await _refreshTokens.IssueAsync(user.Id, refreshToken, cancellationToken);

        return await BuildAuthResultAsync(user, refreshToken, cancellationToken);
    }

    private async Task<AuthResult> BuildAuthResultAsync(FinAiUser user, string refreshToken, CancellationToken cancellationToken)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);

        return new AuthResult(
            user.Id,
            user.Email ?? string.Empty,
            accessToken,
            _options.AccessTokenTtlMinutes * 60,
            refreshToken);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? email : email[..1] + "***" + email[at..];
    }
}
