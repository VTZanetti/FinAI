using System.Diagnostics;

namespace FinAI.Api.Middleware;

/// <summary>
/// Registra duração e status das requisições HTTP nas métricas (finai_http_request_duration_seconds).
/// </summary>
public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown";
            if (route.Length > 100)
                route = route[..100];

            Telemetry.FinAiMetrics.RecordHttpRequest(
                context.Request.Method,
                route,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalSeconds);
        }
    }
}
