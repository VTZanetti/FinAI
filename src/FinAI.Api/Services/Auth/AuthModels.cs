namespace FinAI.Api.Services.Auth;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// Resultado de autenticação — mesmo shape do contrato (register/login/refresh).
/// </summary>
public sealed record AuthResult(
    Guid UserId,
    string Email,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);
