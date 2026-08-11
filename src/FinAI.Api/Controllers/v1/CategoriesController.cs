using FinAI.Api.Common;
using FinAI.Api.DTOs.Categories;
using FinAI.Api.Security;
using FinAI.Api.Services.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(ICategoryService categories, ICurrentUser currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    /// <summary>Cria uma categoria do usuário.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categories.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Lista categorias do usuário + categorias do sistema.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        var result = await _categories.ListAsync(_currentUser.RequireUserId(), search, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.Select(c => c.ToResponse()).ToList())
            : result.ToProblemDetails();
    }

    /// <summary>Obtém uma categoria pelo id (404 se não pertencer ao usuário nem for do sistema).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categories.GetByIdAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Atualiza uma categoria (categorias do sistema são somente leitura — 403).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categories.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.ToResponse())
            : result.ToProblemDetails();
    }

    /// <summary>Exclui uma categoria (409 se tiver transações vinculadas; sistema é protegida).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categories.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}
