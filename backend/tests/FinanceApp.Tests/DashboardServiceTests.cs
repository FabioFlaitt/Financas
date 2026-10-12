using FinanceApp.Application.Dashboard;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class DashboardServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly Guid FoodId = Guid.NewGuid();
    private static readonly Guid RentId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();

    private static (DashboardService service, FinanceApp.Infrastructure.Persistence.AppDbContext db) Build()
    {
        var db = TestDb.NewContext();

        var checking = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 1000m };
        var savings = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Poupança", InitialBalance = 500m };
        db.Accounts.AddRange(checking, savings);

        db.Categories.AddRange(
            new Category { Id = FoodId, UserId = UserId, Name = "Alimentação", ColorHex = "#2E7D32" },
            new Category { Id = RentId, UserId = UserId, Name = "Moradia", ColorHex = "#5E35B1" });

        void Tx(decimal amount, TransactionType type, DateOnly date, TransactionStatus status,
                Guid? category = null, Guid? account = null, Guid? dest = null, Guid? card = null, bool exclude = false)
            => db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Description = $"{type} {amount}",
                Amount = amount,
                Type = type,
                Status = status,
                TransactionDate = date,
                CategoryId = category,
                AccountId = account,
                DestinationAccountId = dest,
                CreditCardId = card,
                ExcludeFromReports = exclude
            });

        // Mês atual (junho/2026)
        Tx(5000m, TransactionType.Income, new(2026, 6, 5), TransactionStatus.Confirmed, account: checking.Id);
        Tx(1500m, TransactionType.Expense, new(2026, 6, 10), TransactionStatus.Confirmed, category: RentId, account: checking.Id);
        Tx(300m, TransactionType.Expense, new(2026, 6, 12), TransactionStatus.Confirmed, category: FoodId, account: checking.Id);
        Tx(200m, TransactionType.Expense, new(2026, 6, 8), TransactionStatus.Confirmed, category: FoodId, card: CardId); // compra no cartão
        Tx(100m, TransactionType.Expense, new(2026, 6, 20), TransactionStatus.Forecast, category: FoodId, account: checking.Id); // previsto futuro
        Tx(400m, TransactionType.Transfer, new(2026, 6, 6), TransactionStatus.Confirmed, account: checking.Id, dest: savings.Id, exclude: true);

        // Mês anterior (maio/2026)
        Tx(800m, TransactionType.Expense, new(2026, 5, 10), TransactionStatus.Confirmed, category: RentId, account: checking.Id);

        db.SaveChanges();

        var service = new DashboardService(db, new FakeCurrentUser(UserId), new FakeClock(Today));
        return (service, db);
    }

    private static DashboardQuery JuneQuery => new() { Year = 2026, Month = 6 };

    [Fact]
    public async Task Total_balance_excludes_credit_card_purchases_and_future_items()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);
        // Saldo atual = todo o histórico confirmado até hoje:
        // base 1500 - 800 (aluguel de maio) + 5000 - 1500 - 300
        // (cartão 200 não afeta o caixa; previsto e transferência líquida = 0)
        result.TotalBalance.Should().Be(3900m);
    }

    [Fact]
    public async Task Income_and_expense_follow_report_rules()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);

        result.MonthIncome.Should().Be(5000m);                 // transferência não é receita
        result.MonthExpense.Should().Be(2100m);                // 1500 + 300 + 200 (cartão) + 100 (previsto)
        result.RealizedExpenses.Should().Be(2000m);            // apenas confirmados
        result.ForecastExpenses.Should().Be(100m);
        result.MonthResult.Should().Be(2900m);
    }

    [Fact]
    public async Task Compares_with_previous_month()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);

        result.PreviousMonthExpense.Should().Be(800m);
        result.ExpenseVsPreviousMonthPercent.Should().Be(162.5m); // (2100-800)/800*100
        result.CommittedIncomePercent.Should().Be(42m);           // 2100/5000*100
    }

    [Fact]
    public async Task Groups_expenses_by_category_sorted_desc()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);

        result.ExpensesByCategory.Should().HaveCount(2);
        result.ExpensesByCategory[0].CategoryName.Should().Be("Moradia");
        result.ExpensesByCategory[0].Amount.Should().Be(1500m);
        result.ExpensesByCategory.Single(c => c.CategoryName == "Alimentação").Amount.Should().Be(600m); // 300+200+100
    }

    [Fact]
    public async Task Projects_end_of_month_balance_with_future_forecasts()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);

        // 3900 - 100 (despesa prevista em 20/06, após hoje)
        result.ProjectedEndOfMonthBalance.Should().Be(3800m);
        result.DailyBalanceEvolution.Should().HaveCount(30); // junho tem 30 dias
        result.DailyBalanceEvolution[^1].Balance.Should().Be(3800m);
    }

    [Fact]
    public async Task Top_expenses_are_ordered_by_amount()
    {
        var (service, _) = Build();
        var result = await service.GetAsync(JuneQuery);

        result.TopExpenses.Should().NotBeEmpty();
        result.TopExpenses[0].Amount.Should().Be(1500m);
        result.TopExpenses.Should().BeInDescendingOrder(t => t.Amount);
    }
}
