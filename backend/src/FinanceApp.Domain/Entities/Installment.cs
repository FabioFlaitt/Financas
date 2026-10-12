using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Representa uma compra parcelada no cartão. Cada parcela vira um <see cref="Transaction"/>
/// alocado na fatura do mês correspondente, mas todas compartilham esta mesma origem
/// para permitir a projeção das parcelas futuras.
/// </summary>
public class Installment : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Valor total da compra.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Quantidade de parcelas.</summary>
    public int NumberOfInstallments { get; set; }

    /// <summary>Valor de cada parcela.</summary>
    public decimal InstallmentAmount { get; set; }

    /// <summary>Data da compra.</summary>
    public DateOnly PurchaseDate { get; set; }

    public Guid? CategoryId { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
