namespace FinAI.Api.Middleware;

/// <summary>
/// Headers de segurança (hardening v1.0): nosniff, frame options, referrer policy, HSTS.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-XSS-Protection"] = "1; mode=block";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // HSTS apenas com HTTPS
        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}
