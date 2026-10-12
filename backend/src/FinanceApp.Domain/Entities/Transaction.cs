using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Lançamento financeiro: receita, despesa, transferência, estorno ou pagamento de fatura.
/// Valores monetários usam <see cref="decimal"/>. A data financeira (<see cref="TransactionDate"/>)
/// é armazenada como data local para preservar o "dia do gasto".
/// </summary>
public class Transaction : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Valor sempre positivo; o sinal é inferido pelo <see cref="Type"/>.</summary>
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Confirmed;

    /// <summary>Data financeira (dia do gasto/recebimento).</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>Data de vencimento, quando aplicável (contas a pagar/receber).</summary>
    public DateOnly? DueDate { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Conta de origem (débito) ou onde a receita entra.</summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>Conta de destino, usada apenas em transferências.</summary>
    public Guid? DestinationAccountId { get; set; }
    public Account? DestinationAccount { get; set; }

    /// <summary>Cartão relacionado, quando a compra foi no crédito.</summary>
    public Guid? CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Fatura a que esta compra pertence (quando no cartão).</summary>
    public Guid? InvoiceId { get; set; }
    public CreditCardInvoice? Invoice { get; set; }

    public string? Merchant { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public ExpenseNature Nature { get; set; } = ExpenseNature.Variable;
    public TransactionOrigin Origin { get; set; } = TransactionOrigin.Manual;

    public RecurrenceFrequency Recurrence { get; set; } = RecurrenceFrequency.None;
    public Guid? RecurringTransactionId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Identificador idempotente. Para itens importados (Open Finance/CSV/OFX) usa-se o id do
    /// provedor ou um hash de valor+data+estabelecimento+conta, evitando duplicidade na reimportação.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Quando verdadeiro, o lançamento não é contabilizado como despesa/receita real nos relatórios
    /// (ex.: transferências entre contas próprias e pagamentos de fatura).
    /// </summary>
    public bool ExcludeFromReports { get; set; }

    /// <summary>Vincula parcelas à compra parcelada que as originou.</summary>
    public Guid? InstallmentId { get; set; }
    public Installment? Installment { get; set; }
}
