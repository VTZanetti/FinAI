namespace FinAI.Api.DTOs;

/// <summary>
/// Envelope de paginação padrão da API.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
