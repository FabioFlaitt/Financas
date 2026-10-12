using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Fatura mensal de um cartão de crédito. Agrega as compras do ciclo e controla o pagamento.
/// </summary>
public class CreditCardInvoice : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Mês de competência da fatura (dia 1 do mês de referência).</summary>
    public DateOnly ReferenceMonth { get; set; }

    public DateOnly ClosingDate { get; set; }
    public DateOnly DueDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    /// <summary>Total das compras que compõem a fatura (calculado).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Quanto já foi pago desta fatura.</summary>
    public decimal PaidAmount { get; set; }

    public DateOnly? PaidDate { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
