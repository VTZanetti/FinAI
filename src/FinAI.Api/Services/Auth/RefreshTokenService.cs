using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;

namespace FinAI.Api.Services.Auth;

public interface IRefreshTokenService
{
    Task<RefreshToken> IssueAsync(Guid userId, string token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RotateAsync(RefreshToken current, string newToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task RevokeFamilyAsync(RefreshToken token, CancellationToken cancellationToken = default);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _options;

    public RefreshTokenService(IRefreshTokenRepository tokens, ITokenService tokenService, IUnitOfWork unitOfWork, Microsoft.Extensions.Options.IOptions<JwtOptions> options)
    {
        _tokens = tokens;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<RefreshToken> IssueAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = _tokenService.HashToken(token),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenTtlDays),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _tokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return refreshToken;
    }

    public async Task<RefreshToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetByHashAsync(tokenHash, cancellationToken);
        return token is { IsActive: true } ? token : null;
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => await _tokens.GetByHashAsync(tokenHash, cancellationToken);

    /// <summary>
    /// Rotação: marca o token atual como revogado (substituído) e emite um novo.
    /// </summary>
    public async Task RotateAsync(RefreshToken current, string newToken, CancellationToken cancellationToken = default)
    {
        current.RevokedAt = DateTimeOffset.UtcNow;

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = current.UserId,
            TokenHash = _tokenService.HashToken(newToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenTtlDays),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Encadeia a família: o novo token aponta para o anterior
        replacement.ReplacedById = current.Id;
        current.ReplacedByTokenId = replacement.Id.ToString();

        _tokens.Update(current);
        await _tokens.AddAsync(replacement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
        _tokens.Update(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reuso detectado: revoga o token usado E toda a cadeia de substituições (família).
    /// </summary>
    public async Task RevokeFamilyAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        var chain = await _tokens.GetChainAsync(token, cancellationToken);

        // Revoga o próprio token (o reusado) + a família
        token.RevokedAt = DateTimeOffset.UtcNow;
        _tokens.Update(token);

        foreach (var item in chain)
        {
            item.RevokedAt = DateTimeOffset.UtcNow;
            _tokens.Update(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
