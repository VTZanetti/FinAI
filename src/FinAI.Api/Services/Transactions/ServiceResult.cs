using FinAI.Api.Common;

namespace FinAI.Api.Services.Transactions;

/// <summary>
/// Resultado de serviço que também carrega metadados de paginação.
/// </summary>
public sealed class ServiceResult<T> : Result<T>
{
    public int TotalItems { get; }
    public int TotalPages { get; }
    public int Page { get; }
    public int PageSize { get; }

    private ServiceResult(T? value, bool isSuccess, ErrorCode error, string? message,
        int totalItems = 0, int totalPages = 0, int page = 1, int pageSize = 20)
        : base(value, isSuccess, error, message)
    {
        TotalItems = totalItems;
        TotalPages = totalPages;
        Page = page;
        PageSize = pageSize;
    }

    public static ServiceResult<T> Success(T value, int totalItems, int totalPages, int page, int pageSize)
        => new(value, true, ErrorCode.None, null, totalItems, totalPages, page, pageSize);

    public new static ServiceResult<T> Failure(ErrorCode error, string? message = null)
        => new(default, false, error, message);
}
