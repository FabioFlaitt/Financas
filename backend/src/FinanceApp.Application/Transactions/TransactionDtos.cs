using FinanceApp.Domain.Enums;

namespace FinanceApp.Application.Transactions;

public record TransactionDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public TransactionStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateOnly TransactionDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? CategoryColorHex { get; init; }
    public Guid? AccountId { get; init; }
    public string? AccountName { get; init; }
    public Guid? DestinationAccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public string? Merchant { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public ExpenseNature Nature { get; init; }
    public TransactionOrigin Origin { get; init; }
    public RecurrenceFrequency Recurrence { get; init; }
    public string? Notes { get; init; }
}

public record CreateTransactionRequest
{
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; } = TransactionType.Expense;
    public TransactionStatus Status { get; init; } = TransactionStatus.Confirmed;
    public DateOnly TransactionDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? DestinationAccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public string? Merchant { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public ExpenseNature Nature { get; init; } = ExpenseNature.Variable;
    public RecurrenceFrequency Recurrence { get; init; } = RecurrenceFrequency.None;
    public string? Notes { get; init; }
}

public record UpdateTransactionRequest : CreateTransactionRequest
{
    public Guid Id { get; init; }
}

public record TransactionListQuery
{
    public int? Year { get; init; }
    public int? Month { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public TransactionType? Type { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
