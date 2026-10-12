using FinanceApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Name).HasMaxLength(150).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(u => u.Culture).HasMaxLength(10);
        b.HasIndex(u => u.Email).IsUnique();
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.Property(a => a.Name).HasMaxLength(120).IsRequired();
        b.Property(a => a.Institution).HasMaxLength(120);
        b.Property(a => a.ColorHex).HasMaxLength(9);
        b.Property(a => a.Icon).HasMaxLength(60);
        b.HasIndex(a => a.UserId);
        b.HasOne(a => a.BankConnection).WithMany().HasForeignKey(a => a.BankConnectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.Property(c => c.ColorHex).HasMaxLength(9);
        b.Property(c => c.Icon).HasMaxLength(60);
        b.HasIndex(c => c.UserId);
        b.HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.Property(t => t.Description).HasMaxLength(200).IsRequired();
        b.Property(t => t.Merchant).HasMaxLength(150);
        b.Property(t => t.Notes).HasMaxLength(1000);
        b.Property(t => t.ExternalId).HasMaxLength(200);

        b.HasIndex(t => new { t.UserId, t.TransactionDate });
        b.HasIndex(t => new { t.UserId, t.Type, t.Status });
        // Idempotência: um mesmo id externo não se repete para o mesmo usuário.
        b.HasIndex(t => new { t.UserId, t.ExternalId })
            .IsUnique()
            .HasFilter(null);

        b.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.DestinationAccount).WithMany().HasForeignKey(t => t.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.CreditCard).WithMany().HasForeignKey(t => t.CreditCardId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.Invoice).WithMany(i => i.Transactions).HasForeignKey(t => t.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.Installment).WithMany(i => i.Transactions).HasForeignKey(t => t.InstallmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> b)
    {
        b.Property(c => c.Name).HasMaxLength(120).IsRequired();
        b.Property(c => c.Nickname).HasMaxLength(120);
        b.Property(c => c.Brand).HasMaxLength(60);
        b.Property(c => c.ColorHex).HasMaxLength(9);
        b.Property(c => c.Icon).HasMaxLength(60);
        b.HasIndex(c => c.UserId);
    }
}

public class CreditCardInvoiceConfiguration : IEntityTypeConfiguration<CreditCardInvoice>
{
    public void Configure(EntityTypeBuilder<CreditCardInvoice> b)
    {
        b.HasIndex(i => new { i.UserId, i.CreditCardId, i.ReferenceMonth }).IsUnique();
        b.HasOne(i => i.CreditCard).WithMany(c => c.Invoices).HasForeignKey(i => i.CreditCardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> b)
    {
        b.Property(i => i.Description).HasMaxLength(200).IsRequired();
        b.HasIndex(i => i.UserId);
        b.HasOne(i => i.CreditCard).WithMany().HasForeignKey(i => i.CreditCardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> b)
    {
        b.HasIndex(x => new { x.UserId, x.Year, x.Month, x.CategoryId }).IsUnique();
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FinancialGoalConfiguration : IEntityTypeConfiguration<FinancialGoal>
{
    public void Configure(EntityTypeBuilder<FinancialGoal> b)
    {
        b.Property(g => g.Name).HasMaxLength(120).IsRequired();
        b.Property(g => g.ColorHex).HasMaxLength(9);
        b.Property(g => g.Icon).HasMaxLength(60);
        b.HasIndex(g => g.UserId);
    }
}

public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> b)
    {
        b.Property(r => r.Description).HasMaxLength(200).IsRequired();
        b.Property(r => r.Notes).HasMaxLength(1000);
        b.HasIndex(r => r.UserId);
    }
}

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> b)
    {
        b.Property(c => c.InstitutionName).HasMaxLength(120).IsRequired();
        b.Property(c => c.ProviderName).HasMaxLength(60).IsRequired();
        b.Property(c => c.ProviderItemId).HasMaxLength(200);
        b.Property(c => c.ConsentId).HasMaxLength(200);
        b.Property(c => c.LastError).HasMaxLength(1000);
        b.HasIndex(c => c.UserId);
    }
}

public class BankSyncHistoryConfiguration : IEntityTypeConfiguration<BankSyncHistory>
{
    public void Configure(EntityTypeBuilder<BankSyncHistory> b)
    {
        b.Property(h => h.Message).HasMaxLength(1000);
        b.HasOne(h => h.BankConnection).WithMany(c => c.SyncHistory).HasForeignKey(h => h.BankConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.Property(n => n.PreferredTime).HasMaxLength(5);
        b.HasIndex(n => new { n.UserId, n.Type }).IsUnique();
    }
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> b)
    {
        b.Property(t => t.Name).HasMaxLength(150).IsRequired();
        b.Property(t => t.Origin).HasMaxLength(150);
        b.Property(t => t.Destination).HasMaxLength(150);
        b.Property(t => t.VehicleName).HasMaxLength(100);
        b.Property(t => t.Notes).HasMaxLength(1000);
        b.HasIndex(t => new { t.UserId, t.Status });
    }
}

public class TripExpenseConfiguration : IEntityTypeConfiguration<TripExpense>
{
    public void Configure(EntityTypeBuilder<TripExpense> b)
    {
        b.Property(e => e.Description).HasMaxLength(200);
        b.HasIndex(e => e.TripId);
        b.HasOne(e => e.Trip).WithMany(t => t.Expenses).HasForeignKey(e => e.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TripLocationPointConfiguration : IEntityTypeConfiguration<TripLocationPoint>
{
    public void Configure(EntityTypeBuilder<TripLocationPoint> b)
    {
        b.HasIndex(p => new { p.TripId, p.RecordedAtUtc });
        b.HasOne(p => p.Trip).WithMany(t => t.LocationPoints).HasForeignKey(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
