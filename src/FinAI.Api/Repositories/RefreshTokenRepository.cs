using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly FinAiDbContext _db;

    public RefreshTokenRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    /// <summary>
    /// Retorna o token e toda a sua família (substituições encadeadas por ReplacedById/ReplacedByTokenId).
    /// </summary>
    public async Task<IReadOnlyList<RefreshToken>> GetChainAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        // Família: tokens que apontam para o token (substituições descendentes)
        var descendants = await _db.RefreshTokens
            .Where(t => t.ReplacedById == token.Id || t.ReplacedByTokenId == token.Id.ToString())
            .ToListAsync(cancellationToken);

        // E ancestrais (tokens que o token atual substituiu) — busca recursiva simples
        var ancestors = new List<RefreshToken>();
        var current = token;
        while (current.ReplacedById.HasValue)
        {
            var parent = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Id == current.ReplacedById, cancellationToken);
            if (parent is null)
                break;
            ancestors.Add(parent);
            current = parent;
        }

        return descendants.Concat(ancestors).Distinct().ToList();
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        => await _db.RefreshTokens.AddAsync(token, cancellationToken);

    public void Update(RefreshToken token) => _db.RefreshTokens.Update(token);
}
