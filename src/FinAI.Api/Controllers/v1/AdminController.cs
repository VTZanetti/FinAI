using FinAI.Api.DTOs.Admin;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Security;
using FinAI.Api.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

/// <summary>
/// Endpoints administrativos — somente papel Admin.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<FinAiUser> _userManager;

    public AdminController(IAuditService audit, ICurrentUser currentUser, UserManager<FinAiUser> userManager)
    {
        _audit = audit;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    /// <summary>Lista logs de auditoria (somente Admin).</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AuditLogs(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _audit.QueryAsync(new AuditLogFilter(userId, action, from, to, page, pageSize), cancellationToken);

        // Auditoria da auditoria
        await _audit.RecordAsync("admin.audit-logs.read", "AuditLog", null, null, cancellationToken);

        return Ok(logs.Select(l => l.ToResponse()).ToList());
    }

    /// <summary>Lista usuários (somente Admin).</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var users = _userManager.Users
            .OrderBy(u => u.CreatedAt)
            .ToList();

        var responses = new List<AdminUserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            responses.Add(new AdminUserResponse(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.CreatedAt, roles.ToList()));
        }

        await _audit.RecordAsync("admin.users.read", "User", null, null, cancellationToken);

        return Ok(responses);
    }
}
