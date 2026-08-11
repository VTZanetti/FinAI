using FinAI.Api.Services.Auth;

namespace FinAI.Api.DTOs.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);

public static class AuthMappings
{
    public static AuthResponse ToResponse(this AuthResult result)
        => new(result.UserId, result.Email, result.AccessToken, result.ExpiresIn, result.RefreshToken);
}
