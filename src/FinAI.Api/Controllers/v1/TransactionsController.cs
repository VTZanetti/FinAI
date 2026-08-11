using FinAI.Api.Common;
using FinAI.Api.DTOs;
using FinAI.Api.DTOs.Transactions;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Security;
using FinAI.Api.Services.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactions;
    private readonly ICurrentUser _currentUser;

    public TransactionsController(ITransactionService transactions, ICurrentUser currentUser)
    {
        _transactions = transactions;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Cria uma transação. Se categoryId for null, a classificação automática (v0.4+)
    /// será aplicada — na v0.1 a categoria fica vazia.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactions.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Lista transações com filtros e paginação.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TransactionListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] TransactionListQuery query, CancellationToken cancellationToken)
    {
        var filter = ToFilter(query);
        var result = await _transactions.ListAsync(_currentUser.RequireUserId(), filter, cancellationToken);

        if (!result.IsSuccess)
            return result.ToProblemDetails();

        return Ok(new PagedResponse<TransactionListItemResponse>(
            result.Value!.Select(t => t.ToListItem()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages));
    }

    /// <summary>Obtém uma transação pelo id (404 se não pertencer ao usuário).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _transactions.GetByIdAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Atualiza uma transação.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactions.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Exclui uma transação.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _transactions.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }

    private static TransactionFilter ToFilter(TransactionListQuery q)
    {
        TransactionType? type = null;
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            type = q.Type.Trim().ToLowerInvariant() switch
            {
                "income" => TransactionType.Income,
                "expense" => TransactionType.Expense,
                _ => null
            };
        }

        return new TransactionFilter(
            AccountId: q.AccountId,
            CategoryId: q.CategoryId,
            Type: type,
            From: q.From,
            To: q.To,
            MinAmount: q.MinAmount,
            MaxAmount: q.MaxAmount,
            Search: q.Search,
            IsRecurring: q.IsRecurring,
            Page: q.Page,
            PageSize: q.PageSize,
            SortBy: q.SortBy,
            SortOrder: q.SortOrder);
    }
}
