using FinAI.Api.Common;
using FinAI.Api.DTOs.Anomalies;
using FinAI.Api.Security;
using FinAI.Api.Services.AnomalyDetection;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/anomalies")]
[Produces("application/json")]
public class AnomaliesController : ControllerBase
{
    private readonly IAnomalyDetectionService _anomalies;
    private readonly ICurrentUser _currentUser;

    public AnomaliesController(IAnomalyDetectionService anomalies, ICurrentUser currentUser)
    {
        _anomalies = anomalies;
        _currentUser = currentUser;
    }

    /// <summary>Lista transações anômalas no período (Z-score padrão; IQR opcional).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AnomalyReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? method = null,
        [FromQuery] Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _anomalies.DetectAsync(_currentUser.RequireUserId(), from, to, method, accountId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Avalia uma transação em tempo real contra o histórico da categoria.</summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(AnomalyCheckResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check([FromBody] AnomalyCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _anomalies.CheckAsync(_currentUser.RequireUserId(), request.Description, request.Amount, request.CategoryId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }
}

public class AnomalyCheckValidator : AbstractValidator<AnomalyCheckRequest>
{
    public AnomalyCheckValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(255);

        RuleFor(x => x.Amount)
            .NotEqual(0m).WithMessage("Amount must not be zero");
    }
}
