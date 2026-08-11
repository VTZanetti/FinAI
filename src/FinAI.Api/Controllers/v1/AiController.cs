using FinAI.Api.Common;
using FinAI.Api.DTOs.AI;
using FinAI.Api.Security;
using FinAI.Api.Services.AI;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/ai")]
[Produces("application/json")]
public class AiController : ControllerBase
{
    private readonly IClassificationService _classification;
    private readonly IFinancialAdvisorService _advisor;
    private readonly ICurrentUser _currentUser;

    public AiController(
        IClassificationService classification,
        IFinancialAdvisorService advisor,
        ICurrentUser currentUser)
    {
        _classification = classification;
        _advisor = advisor;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Classifica uma transação (regras → cache → LLM → fallback). Sempre retorna source.
    /// </summary>
    [HttpPost("classify")]
    [ProducesResponseType(typeof(ClassifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Classify([FromBody] ClassifyRequest request, CancellationToken cancellationToken)
    {
        var result = await _classification.ClassifyAsync(_currentUser.RequireUserId(), request.Description, request.Amount, cancellationToken);
        return Ok(result.ToResponse());
    }

    /// <summary>
    /// Assistente financeiro: pergunta em linguagem natural respondida com base nos dados reais do usuário.
    /// </summary>
    [HttpPost("financial-advisor")]
    [ProducesResponseType(typeof(Services.AI.AdvisorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> FinancialAdvisor([FromBody] Services.AI.AdvisorRequest request, CancellationToken cancellationToken)
    {
        var result = await _advisor.AskAsync(_currentUser.RequireUserId(), request.Question, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                ErrorCode.Validation => result.ToProblemDetails(),
                _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "AI assistant unavailable",
                    Detail = result.Message
                })
            };
        }

        return Ok(new Services.AI.AdvisorResponse(result.Value!.Answer, result.Value.Context, result.Value.Sources));
    }
}

public class ClassifyValidator : AbstractValidator<ClassifyRequest>
{
    public ClassifyValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(200);

        RuleFor(x => x.Amount)
            .NotEqual(0m).WithMessage("Amount must not be zero");
    }
}

public class AdvisorValidator : AbstractValidator<Services.AI.AdvisorRequest>
{
    public AdvisorValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required")
            .MaximumLength(500);
    }
}
