using FinAI.Api.Common;
using FinAI.Api.DTOs;
using FinAI.Api.DTOs.Accounts;
using FinAI.Api.Security;
using FinAI.Api.Services.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;
    private readonly ICurrentUser _currentUser;

    public AccountsController(IAccountService accounts, ICurrentUser currentUser)
    {
        _accounts = accounts;
        _currentUser = currentUser;
    }

    /// <summary>Cria uma conta bancária.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Lista as contas do usuário.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AccountListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _accounts.ListAsync(_currentUser.RequireUserId(), cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblemDetails();

        var items = result.Value!.Select(a => a.ToListItem()).ToList();
        var totalItems = items.Count;
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
        var pagedItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PagedResponse<AccountListItemResponse>(pagedItems, page, pageSize, totalItems, totalPages));
    }

    /// <summary>Obtém uma conta pelo id (404 se não pertencer ao usuário).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accounts.GetByIdAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Atualiza nome, tipo e moeda da conta (initialBalance não é editável).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Exclui uma conta (409 se tiver transações).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accounts.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}
