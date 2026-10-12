using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Usuário do aplicativo. Guarda apenas o hash da senha (nunca a senha em texto puro)
/// e nunca armazena credenciais bancárias.
/// </summary>
public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash + salt da senha (PBKDF2). Nunca a senha em texto puro.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Cultura usada para formatação (padrão pt-BR).</summary>
    public string Culture { get; set; } = "pt-BR";

    /// <summary>Indica se o usuário habilitou o bloqueio por biometria no dispositivo.</summary>
    public bool BiometricLockEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    // Navegação
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<CreditCard> CreditCards { get; set; } = new List<CreditCard>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public ICollection<FinancialGoal> Goals { get; set; } = new List<FinancialGoal>();
    public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = new List<RecurringTransaction>();
    public ICollection<BankConnection> BankConnections { get; set; } = new List<BankConnection>();
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
}
