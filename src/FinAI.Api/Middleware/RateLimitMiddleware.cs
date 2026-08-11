using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using FinAI.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Middleware;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public int GeneralPerMinute { get; set; } = 120;
    public int AiPerMinute { get; set; } = 30;
    public int AuthPerMinute { get; set; } = 5;
    public int AnomaliesCheckPerMinute { get; set; } = 60;
}

/// <summary>
/// Rate limiting por regras simples (janela fixa em memória):
/// - auth (login/register): por IP, limite baixo.
/// - general: por usuário (claim sub).
/// Headers X-RateLimit-*; excesso → 429 + Retry-After.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // key → (contador, resetUtc)
    private static readonly ConcurrentDictionary<string, (int Count, long ResetUtc)> Buckets = new();

    public RateLimitMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        // Permite desativar em testes/infra via configuração
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        var isAuth = path.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
                     || path.StartsWith("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase);

        var limit = isAuth ? _options.AuthPerMinute : _options.GeneralPerMinute;

        // Chave: usuário autenticado (sub) ou IP para auth
        string key;
        if (isAuth)
        {
            key = "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
        else
        {
            var sub = currentUser.UserId?.ToString() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            key = "user:" + sub;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - 60;

        Buckets.AddOrUpdate(key, (1, now + 60), (_, existing) =>
        {
            if (existing.ResetUtc < now)
                return (1, now + 60);
            return (existing.Count + 1, existing.ResetUtc);
        });

        var bucket = Buckets[key];
        var remaining = Math.Max(0, limit - bucket.Count);

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = bucket.ResetUtc.ToString();

        if (bucket.Count > limit)
        {
            _logger.LogWarning("Rate limit exceeded for {Key} on {Method} {Path}", MaskKey(key), method, path);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers.RetryAfter = (bucket.ResetUtc - now).ToString();

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Type = "https://httpstatuses.com/429",
                Detail = $"Rate limit exceeded. Retry after {bucket.ResetUtc - now} seconds."
            };

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        await _next(context);
    }

    private static string MaskKey(string key)
    {
        var prefix = key.StartsWith("ip:") ? "ip" : "user";
        return $"{prefix}:***";
    }
}
