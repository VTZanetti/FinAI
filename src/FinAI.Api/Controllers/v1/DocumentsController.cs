using FinAI.Api.Common;
using FinAI.Api.DTOs.Documents;
using FinAI.Api.Models;
using FinAI.Api.Security;
using FinAI.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly IDocumentProcessor _processor;
    private readonly ICurrentUser _currentUser;

    public DocumentsController(IDocumentService documents, IDocumentProcessor processor, ICurrentUser currentUser)
    {
        _documents = documents;
        _processor = processor;
        _currentUser = currentUser;
    }

    /// <summary>Faz upload de um documento (PDF/texto) para o pipeline RAG.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Detail = "A file is required" });

        await using var stream = file.OpenReadStream();
        var result = await _documents.UploadAsync(_currentUser.RequireUserId(), file.FileName, file.ContentType, stream, cancellationToken);

        if (!result.IsSuccess)
            return result.ToProblemDetails();

        _processor.Enqueue(result.Value!.Id, _currentUser.RequireUserId());

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value.ToResponse());
    }

    /// <summary>Lista os documentos do usuário.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _documents.ListAsync(_currentUser.RequireUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.Select(d => d.ToResponse()).ToList())
            : result.ToProblemDetails();
    }

    /// <summary>Obtém um documento pelo id (404 se não pertencer ao usuário).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.GetByIdAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Exclui um documento (arquivo + chunks + embeddings).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}
