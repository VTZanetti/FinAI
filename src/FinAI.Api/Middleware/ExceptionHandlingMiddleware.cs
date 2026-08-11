using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace FinAI.Api.Middleware;

/// <summary>
/// Converte exceções não tratadas em <see cref="ProblemDetails"/> (500, sem detalhes internos).
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path} (TraceId: {TraceId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Type = "https://httpstatuses.com/500",
                Detail = "An unexpected error occurred. Please try again later."
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
