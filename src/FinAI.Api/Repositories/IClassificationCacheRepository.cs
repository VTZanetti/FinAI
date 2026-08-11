using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IClassificationCacheRepository
{
    Task<ClassificationCache?> FindSimilarAsync(Guid userId, string normalizedDescription, string amountBucket, CancellationToken cancellationToken = default);
    Task AddAsync(ClassificationCache entry, CancellationToken cancellationToken = default);
    void Update(ClassificationCache entry);
}
