using FinanceApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Common.Interfaces;

/// <summary>
/// Abstração do contexto de dados exposta à camada de aplicação. Permite escrever casos de uso
/// com LINQ/EF Core sem acoplar diretamente à implementação concreta em Infrastructure
/// (que também facilita os testes com o provedor InMemory).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Category> Categories { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<CreditCard> CreditCards { get; }
    DbSet<CreditCardInvoice> CreditCardInvoices { get; }
    DbSet<Installment> Installments { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<FinancialGoal> Goals { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<BankConnection> BankConnections { get; }
    DbSet<BankSyncHistory> BankSyncHistory { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<Trip> Trips { get; }
    DbSet<TripExpense> TripExpenses { get; }
    DbSet<TripLocationPoint> TripLocationPoints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
