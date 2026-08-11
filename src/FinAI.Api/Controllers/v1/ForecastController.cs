using FinAI.Api.Common;
using FinAI.Api.DTOs.Forecasting;
using FinAI.Api.Security;
using FinAI.Api.Services.Forecasting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/forecast")]
[Produces("application/json")]
public class ForecastController : ControllerBase
{
    private readonly IForecastService _forecast;
    private readonly ICurrentUser _currentUser;

    public ForecastController(IForecastService forecast, ICurrentUser currentUser)
    {
        _forecast = forecast;
        _currentUser = currentUser;
    }

    /// <summary>Previsão de fluxo de caixa (média móvel ponderada) para os próximos meses.</summary>
    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(CashFlowForecastResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CashFlow([FromQuery] int months = 6, [FromQuery] Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        var result = await _forecast.GetCashFlowForecastAsync(_currentUser.RequireUserId(), months, accountId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }
}
