using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class ClassificationCacheRepository : IClassificationCacheRepository
{
    private readonly FinAiDbContext _db;

    public ClassificationCacheRepository(FinAiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Busca por prefixo da descrição normalizada (similaridade simples): as 3 primeiras palavras.
    /// </summary>
    public async Task<ClassificationCache?> FindSimilarAsync(Guid userId, string normalizedDescription, string amountBucket, CancellationToken cancellationToken = default)
    {
        var words = normalizedDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3);
        var prefix = string.Join(' ', words);

        if (prefix.Length < 4)
            return null;

        return await _db.ClassificationCaches
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.AmountBucket == amountBucket && c.NormalizedDescription.StartsWith(prefix))
            .OrderByDescending(c => c.HitCount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(ClassificationCache entry, CancellationToken cancellationToken = default)
        => await _db.ClassificationCaches.AddAsync(entry, cancellationToken);

    public void Update(ClassificationCache entry) => _db.ClassificationCaches.Update(entry);
}
