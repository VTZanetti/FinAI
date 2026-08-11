using FinAI.Api.Common;
using FinAI.Api.DTOs.AI;
using FinAI.Api.Security;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.AI.External;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Route("api/v1/providers")]
[Produces("application/json")]
public class ProvidersController : ControllerBase
{
    private readonly IExternalProviderRegistry _registry;
    private readonly IExternalLlmProviderFactory _factory;
    private readonly IClassificationService _classification;
    private readonly ICurrentUser _currentUser;

    public ProvidersController(
        IExternalProviderRegistry registry,
        IExternalLlmProviderFactory factory,
        IClassificationService classification,
        ICurrentUser currentUser)
    {
        _registry = registry;
        _factory = factory;
        _classification = classification;
        _currentUser = currentUser;
    }

    /// <summary>Registra/atualiza um provider externo (somente Admin). Chave via env var, nunca no body persistido.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Upsert([FromBody] UpsertProviderRequest request)
    {
        var config = new ExternalProviderConfig
        {
            Name = request.Name.Trim(),
            Type = request.Type,
            BaseUrl = request.BaseUrl.Trim(),
            Model = request.Model.Trim(),
            ApiKeyEnvVar = request.ApiKeyEnvVar?.Trim() ?? string.Empty,
            Enabled = request.Enabled
        };

        _registry.Upsert(config);
        return CreatedAtAction(nameof(Get), new { name = config.Name }, config.ToSafeDto());
    }

    /// <summary>Lista providers cadastrados (sem expor chaves).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult List()
        => Ok(_registry.List().Select(p => p.ToSafeDto()).ToList());

    /// <summary>Detalhe de um provider (sem expor chaves).</summary>
    [HttpGet("{name}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(string name)
    {
        var config = _registry.Get(name);
        return config is null
            ? NotFound()
            : Ok(config.ToSafeDto());
    }

    /// <summary>Remove um provider (somente Admin).</summary>
    [HttpDelete("{name}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(string name)
        => _registry.Remove(name) ? NoContent() : NotFound();

    /// <summary>Proxy de chat para o provider externo (qualquer usuário autenticado).</summary>
    [HttpPost("{name}/chat")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Chat(string name, [FromBody] ExternalChatRequest request, CancellationToken cancellationToken)
    {
        var provider = _factory.Create(name);
        if (provider is null)
            return NotFound();

        var response = await provider.CompleteChatAsync(new LlmChatRequest(
            "Você é um assistente útil. Responda de forma clara e concisa.",
            request.Message), cancellationToken);

        return response.Success
            ? Ok(new { answer = response.Content, source = "external" })
            : StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "External provider unavailable",
                Detail = response.Error
            });
    }

    /// <summary>Classifica usando o provider externo (delega ao pipeline, source: external).</summary>
    [HttpPost("{name}/classify")]
    [Authorize]
    [ProducesResponseType(typeof(ClassifyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Classify(string name, [FromBody] ClassifyRequest request, CancellationToken cancellationToken)
    {
        var provider = _factory.Create(name);
        if (provider is null)
            return NotFound();

        var result = await _classification.ClassifyAsync(_currentUser.RequireUserId(), request.Description, request.Amount, cancellationToken);
        return Ok(result.ToResponse());
    }
}

public sealed record UpsertProviderRequest(string Name, ExternalProviderType Type, string BaseUrl, string Model, string? ApiKeyEnvVar = null, bool Enabled = true);

public sealed record ExternalChatRequest(string Message);

public class UpsertProviderValidator : AbstractValidator<UpsertProviderRequest>
{
    public UpsertProviderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BaseUrl).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out _));
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
    }
}

public class ExternalChatValidator : AbstractValidator<ExternalChatRequest>
{
    public ExternalChatValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}