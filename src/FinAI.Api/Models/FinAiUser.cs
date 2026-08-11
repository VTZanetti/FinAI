using Microsoft.AspNetCore.Identity;

namespace FinAI.Api.Models;

/// <summary>
/// Usuário do FinAI (ASP.NET Core Identity).
/// </summary>
public class FinAiUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
