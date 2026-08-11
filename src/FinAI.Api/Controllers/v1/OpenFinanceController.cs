using FinAI.Api.Common;
using FinAI.Api.Security;
using FinAI.Api.Services.OpenFinance;
using FinAI.Api.Services.OpenFinance.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/open-finance")]
[Produces("application/json")]
public class OpenFinanceController : ControllerBase
{
    private readonly IOpenFinanceSyncService _sync;
    private readonly IOpenFinanceConnectionService _connections;
    private readonly IOpenFinanceStatusService _status;
    private readonly ICurrentUser _currentUser;

    public OpenFinanceController(
        IOpenFinanceSyncService sync,
        IOpenFinanceConnectionService connections,
        IOpenFinanceStatusService status,
        ICurrentUser currentUser)
    {
        _sync = sync;
        _connections = connections;
        _status = status;
        _currentUser = currentUser;
    }

    /// <summary>Sincroniza agora (Modo A: ItemId configurado; Modo B: itemId da conexão).</summary>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync([FromBody] SyncRequest? request = null, CancellationToken cancellationToken = default)
    {
        var result = await _sync.SyncAsync(_currentUser.RequireUserId(), request?.ItemId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblemDetails();
    }
    /// <summary>Status do último sync + contagem de conexões.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var result = await _status.GetStatusAsync(_currentUser.RequireUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblemDetails();
    }

    /// <summary>Modo B: gera Connect Token para o Connect Widget (proxy do POST /connect-token da Pluggy).</summary>
    [HttpPost("connect-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConnectToken(CancellationToken cancellationToken)
    {
        var result = await _connections.CreateConnectTokenAsync(_currentUser.RequireUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblemDetails();
    }

    /// <summary>Modo B: vincula o itemId retornado pelo widget ao usuário autenticado.</summary>
    [HttpPost("connections")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> LinkConnection([FromBody] LinkConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _connections.LinkConnectionAsync(_currentUser.RequireUserId(), request.ItemId, request.InstitutionName, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ListConnections), new { }, result.Value)
            : result.ToProblemDetails();
    }

    /// <summary>Modo B: lista conexões do usuário.</summary>
    [HttpGet("connections")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListConnections(CancellationToken cancellationToken)
    {
        var result = await _connections.ListConnectionsAsync(_currentUser.RequireUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblemDetails();
    }
}

public sealed record SyncRequest(string? ItemId = null);

public sealed record LinkConnectionRequest(string ItemId, string? InstitutionName = null);

public class LinkConnectionValidator : AbstractValidator<LinkConnectionRequest>
{
    public LinkConnectionValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().MaximumLength(120);
    }
}