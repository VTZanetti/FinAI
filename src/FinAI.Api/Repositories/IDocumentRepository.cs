using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IDocumentRepository
{
    Task<FinancialDocument?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialDocument>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialDocument document, CancellationToken cancellationToken = default);
    void Update(FinancialDocument document);
    void Delete(FinancialDocument document);
}
