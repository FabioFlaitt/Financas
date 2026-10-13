using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace FinanceApp.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CreditCardInvoice> CreditCardInvoices => Set<CreditCardInvoice>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<FinancialGoal> Goals => Set<FinancialGoal>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankSyncHistory> BankSyncHistory => Set<BankSyncHistory>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripExpense> TripExpenses => Set<TripExpense>();
    public DbSet<TripLocationPoint> TripLocationPoints => Set<TripLocationPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Precisão padrão para todos os valores monetários (numeric(18,2)), evitando
        // truncamento silencioso e avisos do EF Core.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // SQLite (modo local no celular) não agrega/ordena o tipo decimal: guarda como double.
        // O arredondamento monetário continua sendo feito pelas regras da aplicação.
        if (Database.IsSqlite())
        {
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                // Para decimal? o EF adapta automaticamente o conversor de decimal.
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal, double>(
                    v => (double)v, v => (decimal)v));
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
