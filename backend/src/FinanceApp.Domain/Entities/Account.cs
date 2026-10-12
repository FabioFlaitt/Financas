using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Conta bancária ou carteira do usuário. O saldo atual é derivado do saldo inicial
/// somado aos lançamentos confirmados.
/// </summary>
public class Account : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Nome da instituição (ex.: "Itaú", "Nubank").</summary>
    public string? Institution { get; set; }

    public AccountType Type { get; set; } = AccountType.Checking;

    /// <summary>Saldo de abertura informado ao cadastrar a conta.</summary>
    public decimal InitialBalance { get; set; }

    public string ColorHex { get; set; } = "#1565C0";
    public string Icon { get; set; } = "bank";

    /// <summary>Se false, a conta não entra no saldo consolidado do dashboard.</summary>
    public bool IncludeInTotalBalance { get; set; } = true;

    public bool IsArchived { get; set; }

    /// <summary>Preenchido quando a conta provém de uma integração bancária.</summary>
    public Guid? BankConnectionId { get; set; }
    public BankConnection? BankConnection { get; set; }
}
