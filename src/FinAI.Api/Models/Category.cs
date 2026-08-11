namespace FinAI.Api.Models;

/// <summary>
/// Categoria de classificação de transações.
/// Categorias do sistema usam <see cref="UserId"/> = <see cref="Guid.Empty"/> e não podem ser editadas/excluídas.
/// </summary>
public class Category
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public bool IsSystem { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
