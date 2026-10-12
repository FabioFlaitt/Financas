using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Categoria de classificação de lançamentos (ex.: Moradia, Alimentação).
/// Suporta subcategorias por auto-relacionamento (<see cref="ParentCategoryId"/>),
/// cor e ícone personalizáveis.
/// </summary>
public class Category : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Cor em hexadecimal (ex.: "#2E7D32").</summary>
    public string ColorHex { get; set; } = "#607D8B";

    /// <summary>Nome/código do ícone usado na interface.</summary>
    public string Icon { get; set; } = "tag";

    /// <summary>Natureza padrão dos gastos desta categoria (fixa/variável).</summary>
    public ExpenseNature DefaultNature { get; set; } = ExpenseNature.Variable;

    /// <summary>Indica se é uma categoria padrão do sistema (semeada no cadastro).</summary>
    public bool IsSystem { get; set; }

    public bool IsArchived { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
}
