using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly FinAiDbContext _db;

    public AuditLogRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
        => await _db.AuditLogs.AddAsync(log, cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(l => l.UserId == filter.UserId);

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(l => l.Action == filter.Action);

        if (filter.From.HasValue)
            query = query.Where(l => l.OccurredAt >= filter.From);

        if (filter.To.HasValue)
            query = query.Where(l => l.OccurredAt <= filter.To);

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .Skip((Math.Max(1, filter.Page) - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);
    }
}
