using FinAI.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Services;

/// <summary>
/// Unidade de trabalho — encapsula o SaveChanges do DbContext.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class EfUnitOfWork : IUnitOfWork
{
    private readonly FinAiDbContext _db;

    public EfUnitOfWork(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
