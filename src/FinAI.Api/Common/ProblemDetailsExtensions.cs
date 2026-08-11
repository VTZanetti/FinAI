using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Common;

/// <summary>
/// Extensões para converter <see cref="Result"/> em <see cref="ProblemDetails"/> (RFC 7807).
/// </summary>
public static class ProblemDetailsExtensions
{
    public static ObjectResult ToProblemDetails(this Result result)
    {
        var (status, title) = result.Error switch
        {
            ErrorCode.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorCode.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorCode.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorCode.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "Business rule violation"),
            ErrorCode.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorCode.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}",
            Detail = result.Message
        };

        problem.Extensions["errorCode"] = result.Error.ToString();

        return new ObjectResult(problem) { StatusCode = status };
    }

    public static ObjectResult ToProblemDetails<T>(this Result<T> result)
        => ((Result)result).ToProblemDetails();
}
