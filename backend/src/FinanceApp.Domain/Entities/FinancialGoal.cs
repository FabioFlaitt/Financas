using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Meta financeira ou reserva (ex.: "Reserva de emergência", "Viagem").
/// </summary>
public class FinancialGoal : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }

    /// <summary>Valor já acumulado em direção à meta.</summary>
    public decimal CurrentAmount { get; set; }

    public DateOnly? TargetDate { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.InProgress;

    public string ColorHex { get; set; } = "#00897B";
    public string Icon { get; set; } = "target";

    /// <summary>Conta vinculada à reserva, quando houver.</summary>
    public Guid? LinkedAccountId { get; set; }
}
