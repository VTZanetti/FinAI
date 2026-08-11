using FinAI.Api.Common;
using FinAI.Api.DTOs.Budgets;
using FinAI.Api.Security;
using FinAI.Api.Services.Budgets;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Route("api/v1/budgets")]
[Produces("application/json")]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgets;
    private readonly ICurrentUser _currentUser;

    public BudgetsController(IBudgetService budgets, ICurrentUser currentUser)
    {
        _budgets = budgets;
        _currentUser = currentUser;
    }

    /// <summary>Cria um orçamento por categoria para um mês/ano.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var result = await _budgets.CreateAsync(_currentUser.UserId, request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblemDetails();

        var spent = await _budgets.GetSpentAmountAsync(_currentUser.UserId, result.Value!, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value!.ToResponse(spent));
    }

    /// <summary>Lista orçamentos (filtro opcional por mês/ano).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? month = null, [FromQuery] int? year = null, CancellationToken cancellationToken = default)
    {
        var result = await _budgets.ListAsync(_currentUser.UserId, month, year, cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblemDetails();

        var responses = new List<BudgetResponse>();
        foreach (var budget in result.Value!)
        {
            var spent = await _budgets.GetSpentAmountAsync(_currentUser.UserId, budget, cancellationToken);
            responses.Add(budget.ToResponse(spent));
        }

        return Ok(responses);
    }

    /// <summary>Obtém um orçamento com gasto e progresso calculados.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _budgets.GetByIdAsync(_currentUser.UserId, id, cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblemDetails();

        var spent = await _budgets.GetSpentAmountAsync(_currentUser.UserId, result.Value!, cancellationToken);
        return Ok(result.Value!.ToResponse(spent));
    }

    /// <summary>Atualiza o limite de um orçamento.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var result = await _budgets.UpdateAsync(_currentUser.UserId, id, request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblemDetails();

        var spent = await _budgets.GetSpentAmountAsync(_currentUser.UserId, result.Value!, cancellationToken);
        return Ok(result.Value!.ToResponse(spent));
    }

    /// <summary>Exclui um orçamento.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _budgets.DeleteAsync(_currentUser.UserId, id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}
