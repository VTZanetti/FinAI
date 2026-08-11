using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly FinAiDbContext _db;

    public DocumentRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<FinancialDocument?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<FinancialDocument>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(FinancialDocument document, CancellationToken cancellationToken = default)
        => await _db.Documents.AddAsync(document, cancellationToken);

    public void Update(FinancialDocument document) => _db.Documents.Update(document);

    public void Delete(FinancialDocument document) => _db.Documents.Remove(document);
}
