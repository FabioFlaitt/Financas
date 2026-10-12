using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Modelo (template) de lançamento recorrente: aluguel, energia, salário, assinaturas etc.
/// A partir dele são gerados lançamentos futuros no fluxo de caixa projetado.
/// </summary>
public class RecurringTransaction : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CreditCardId { get; set; }

    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    /// <summary>Dia do mês/semana de referência para gerar o próximo lançamento.</summary>
    public int DayOfPeriod { get; set; } = 1;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Data a partir da qual o próximo lançamento deve ser gerado.</summary>
    public DateOnly? NextOccurrence { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}
