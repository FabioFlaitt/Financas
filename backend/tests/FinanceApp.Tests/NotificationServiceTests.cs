using FinanceApp.Application.Budgets;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Notifications;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class AlertsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly Guid FoodId = Guid.NewGuid();

    private static AlertsService Build(params NotificationType[] enabledTypes)
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 0m };
        var card = new CreditCard { Id = Guid.NewGuid(), UserId = UserId, Name = "Nubank", ClosingDay = 3, DueDay = 18 };
        db.Accounts.Add(account);
        db.Categories.Add(new Category { Id = FoodId, UserId = UserId, Name = "Alimentação", ColorHex = "#2E7D32" });
        db.CreditCards.Add(card);

        // Orçamento estourado: limite 100, gasto confirmado 150.
        db.Budgets.Add(new Budget { UserId = UserId, CategoryId = FoodId, Year = 2026, Month = 6, LimitAmount = 100m, AlertThresholdPercent = 80 });
        db.Transactions.Add(new Transaction
        {
            UserId = UserId, Description = "Mercado", Amount = 150m, Type = TransactionType.Expense,
            Status = TransactionStatus.Confirmed, TransactionDate = new DateOnly(2026, 6, 10), CategoryId = FoodId
        });

        // Fatura a vencer em 18/06 (dentro de 7 dias).
        db.CreditCardInvoices.Add(new CreditCardInvoice
        {
            UserId = UserId, CreditCardId = card.Id, ReferenceMonth = new DateOnly(2026, 6, 1),
            ClosingDate = new DateOnly(2026, 6, 3), DueDate = new DateOnly(2026, 6, 18),
            Status = InvoiceStatus.Open, TotalAmount = 200m, PaidAmount = 0m
        });

        // Conta a pagar prevista para 17/06.
        db.Transactions.Add(new Transaction
        {
            UserId = UserId, Description = "Luz", Amount = 80m, Type = TransactionType.Expense,
            Status = TransactionStatus.Forecast, TransactionDate = new DateOnly(2026, 6, 17),
            DueDate = new DateOnly(2026, 6, 17), AccountId = account.Id
        });

        foreach (var type in enabledTypes)
            db.NotificationPreferences.Add(new NotificationPreference { UserId = UserId, Type = type, Enabled = true });

        db.SaveChanges();

        var currentUser = new FakeCurrentUser(UserId);
        var budgets = new BudgetService(db, currentUser);
        return new AlertsService(db, currentUser, new FakeClock(Today), budgets);
    }

    [Fact]
    public async Task Produces_budget_invoice_and_bill_alerts_when_enabled()
    {
        var service = Build(NotificationType.ExpenseOverLimit, NotificationType.InvoiceDue, NotificationType.BillDue);

        var alerts = await service.GetAsync();

        alerts.Should().Contain(a => a.Type == NotificationType.ExpenseOverLimit && a.Severity == "critical");
        alerts.Should().Contain(a => a.Type == NotificationType.InvoiceDue);
        alerts.Should().Contain(a => a.Type == NotificationType.BillDue);
        // ordenados por severidade: o crítico (orçamento estourado) vem primeiro
        alerts[0].Severity.Should().Be("critical");
    }

    [Fact]
    public async Task Disabled_preferences_suppress_their_alerts()
    {
        // Apenas faturas habilitadas: não deve haver alerta de orçamento nem de conta a pagar.
        var service = Build(NotificationType.InvoiceDue);

        var alerts = await service.GetAsync();

        alerts.Should().OnlyContain(a => a.Type == NotificationType.InvoiceDue);
    }

    [Fact]
    public async Task No_preferences_means_no_alerts()
    {
        var service = Build();
        (await service.GetAsync()).Should().BeEmpty();
    }
}

public class NotificationPreferenceServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (NotificationPreferenceService service, AppDbContext db) Build()
    {
        var db = TestDb.NewContext();
        return (new NotificationPreferenceService(db, new FakeCurrentUser(UserId)), db);
    }

    [Fact]
    public async Task List_creates_all_types_when_missing()
    {
        var (service, _) = Build();

        var prefs = await service.ListAsync();

        prefs.Should().HaveCount(Enum.GetValues<NotificationType>().Length);
        prefs.Should().OnlyContain(p => !string.IsNullOrEmpty(p.TypeLabel));
    }

    [Fact]
    public async Task Update_toggles_enabled_and_sets_time()
    {
        var (service, _) = Build();

        var updated = await service.UpdateAsync(NotificationType.WeeklySummary, new UpdateNotificationPreferenceRequest
        { Enabled = false, HideSensitiveOnLockScreen = true, PreferredTime = "09:30" });

        updated.Enabled.Should().BeFalse();
        updated.PreferredTime.Should().Be("09:30");
    }

    [Fact]
    public async Task Update_rejects_invalid_time()
    {
        var (service, _) = Build();

        var act = () => service.UpdateAsync(NotificationType.WeeklySummary, new UpdateNotificationPreferenceRequest
        { Enabled = true, HideSensitiveOnLockScreen = true, PreferredTime = "25:99" });

        await act.Should().ThrowAsync<AppValidationException>();
    }
}
