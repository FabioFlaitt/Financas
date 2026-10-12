using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Orçamento mensal. Quando <see cref="CategoryId"/> é nulo representa o orçamento total do mês;
/// caso contrário, o limite daquela categoria.
/// </summary>
public class Budget : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    /// <summary>Nulo = orçamento total do mês.</summary>
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Ano de referência.</summary>
    public int Year { get; set; }

    /// <summary>Mês de referência (1-12).</summary>
    public int Month { get; set; }

    /// <summary>Limite de gastos definido para o período.</summary>
    public decimal LimitAmount { get; set; }

    /// <summary>Percentual (0-100) a partir do qual o usuário quer ser alertado.</summary>
    public int AlertThresholdPercent { get; set; } = 80;
}
