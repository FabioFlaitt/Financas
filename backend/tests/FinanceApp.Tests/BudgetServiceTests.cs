using FinanceApp.Application.Budgets;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class BudgetServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FoodId = Guid.NewGuid();
    private static readonly Guid RentId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();

    private static (BudgetService service, FinanceApp.Infrastructure.Persistence.AppDbContext db) Build()
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 0m };
        db.Accounts.Add(account);
        db.Categories.AddRange(
            new Category { Id = FoodId, UserId = UserId, Name = "Alimentação", ColorHex = "#2E7D32" },
            new Category { Id = RentId, UserId = UserId, Name = "Moradia", ColorHex = "#5E35B1" });

        void Expense(decimal amount, Guid category, DateOnly date, TransactionStatus status, Guid? card = null)
            => db.Transactions.Add(new Transaction
            {
                UserId = UserId,
                Description = "Despesa",
                Amount = amount,
                Type = TransactionType.Expense,
                Status = status,
                TransactionDate = date,
                CategoryId = category,
                AccountId = card == null ? account.Id : null,
                CreditCardId = card
            });

        // Junho/2026 — realizadas (contam para o consumo)
        Expense(1500m, RentId, new(2026, 6, 10), TransactionStatus.Confirmed);
        Expense(300m, FoodId, new(2026, 6, 12), TransactionStatus.Confirmed);
        Expense(200m, FoodId, new(2026, 6, 8), TransactionStatus.Confirmed, card: CardId); // compra no cartão conta
        // Prevista — NÃO conta como realizada
        Expense(100m, FoodId, new(2026, 6, 20), TransactionStatus.Forecast);
        // Receita e transferência não entram no consumo de despesa
        db.Transactions.Add(new Transaction
        {
            UserId = UserId, Description = "Salário", Amount = 5000m, Type = TransactionType.Income,
            Status = TransactionStatus.Confirmed, TransactionDate = new(2026, 6, 5), AccountId = account.Id
        });

        db.Budgets.AddRange(
            new Budget { UserId = UserId, CategoryId = null, Year = 2026, Month = 6, LimitAmount = 3000m, AlertThresholdPercent = 80 },
            new Budget { UserId = UserId, CategoryId = RentId, Year = 2026, Month = 6, LimitAmount = 1000m, AlertThresholdPercent = 80 },
            new Budget { UserId = UserId, CategoryId = FoodId, Year = 2026, Month = 6, LimitAmount = 500m, AlertThresholdPercent = 80 });

        db.SaveChanges();
        return (new BudgetService(db, new FakeCurrentUser(UserId)), db);
    }

    [Fact]
    public async Task Overview_sums_limits_and_realized_spending()
    {
        var (service, _) = Build();
        var overview = await service.GetOverviewAsync(2026, 6);

        overview.HasTotalBudget.Should().BeTrue();
        overview.TotalLimit.Should().Be(3000m);
        overview.TotalSpent.Should().Be(2000m);       // 1500 + 300 + 200 (previsto 100 não conta)
        overview.TotalRemaining.Should().Be(1000m);
        overview.UsedPercent.Should().Be(66.7m);
        overview.Items.Should().HaveCount(3);
        overview.Items[0].CategoryId.Should().BeNull(); // orçamento total vem primeiro
    }

    [Fact]
    public async Task Category_budget_counts_only_realized_expenses()
    {
        var (service, _) = Build();
        var overview = await service.GetOverviewAsync(2026, 6);

        var food = overview.Items.Single(i => i.CategoryId == FoodId);
        food.SpentAmount.Should().Be(500m);   // 300 + 200 (cartão); 100 previsto não entra
        food.UsedPercent.Should().Be(100m);
        food.IsOverLimit.Should().BeFalse();   // 500 não é > 500
        food.IsAlert.Should().BeTrue();        // 100% >= 80%
        food.RemainingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Over_limit_budget_is_flagged()
    {
        var (service, _) = Build();
        var overview = await service.GetOverviewAsync(2026, 6);

        var rent = overview.Items.Single(i => i.CategoryId == RentId);
        rent.SpentAmount.Should().Be(1500m);
        rent.UsedPercent.Should().Be(150m);
        rent.IsOverLimit.Should().BeTrue();
        rent.RemainingAmount.Should().Be(-500m);
    }

    [Fact]
    public async Task Create_rejects_duplicate_period_and_category()
    {
        var (service, _) = Build();
        var request = new SaveBudgetRequest { CategoryId = FoodId, Year = 2026, Month = 6, LimitAmount = 700m };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task Create_update_and_delete_roundtrip()
    {
        var (service, _) = Build();

        var created = await service.CreateAsync(new SaveBudgetRequest
        { CategoryId = null, Year = 2026, Month = 7, LimitAmount = 2500m, AlertThresholdPercent = 90 });
        created.Id.Should().NotBeEmpty();
        created.AlertThresholdPercent.Should().Be(90);

        var updated = await service.UpdateAsync(created.Id, new SaveBudgetRequest
        { CategoryId = null, Year = 2026, Month = 7, LimitAmount = 2800m, AlertThresholdPercent = 75 });
        updated.LimitAmount.Should().Be(2800m);
        updated.AlertThresholdPercent.Should().Be(75);

        await service.DeleteAsync(created.Id);
        var julyOverview = await service.GetOverviewAsync(2026, 7);
        julyOverview.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_rejects_invalid_limit()
    {
        var (service, _) = Build();
        var request = new SaveBudgetRequest { CategoryId = null, Year = 2026, Month = 8, LimitAmount = 0m };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(request));
    }
}
