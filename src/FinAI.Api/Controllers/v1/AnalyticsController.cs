using FinAI.Api.Common;
using FinAI.Api.DTOs.Analytics;
using FinAI.Api.Security;
using FinAI.Api.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/analytics")]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly ICurrentUser _currentUser;

    public AnalyticsController(IAnalyticsService analytics, ICurrentUser currentUser)
    {
        _analytics = analytics;
        _currentUser = currentUser;
    }

    /// <summary>Resumo de gastos: totais, composição por categoria e recorrência (período explícito).</summary>
    [HttpGet("spending-summary")]
    [ProducesResponseType(typeof(SpendingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SpendingSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _analytics.GetSpendingSummaryAsync(_currentUser.RequireUserId(), from, to, accountId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Análise de comportamento: variações por categoria e insights (últimos N meses).</summary>
    [HttpGet("behavior")]
    [ProducesResponseType(typeof(BehaviorReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Behavior(
        [FromQuery] int months = 3,
        [FromQuery] Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _analytics.GetBehaviorAsync(_currentUser.RequireUserId(), months, accountId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Série mensal de receitas/despesas para gráficos.</summary>
    [HttpGet("monthly-trend")]
    [ProducesResponseType(typeof(MonthlyTrendReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MonthlyTrend(
        [FromQuery] int months = 12,
        [FromQuery] Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _analytics.GetMonthlyTrendAsync(_currentUser.RequireUserId(), months, accountId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }
}
