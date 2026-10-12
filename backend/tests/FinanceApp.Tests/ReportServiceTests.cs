using FinanceApp.Application.Reports;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class ReportServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FoodId = Guid.NewGuid();
    private static readonly Guid RentId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();

    private static ReportService Build()
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 0m };
        db.Accounts.Add(account);
        db.Categories.AddRange(
            new Category { Id = FoodId, UserId = UserId, Name = "Alimentação", ColorHex = "#2E7D32" },
            new Category { Id = RentId, UserId = UserId, Name = "Moradia", ColorHex = "#5E35B1" });

        void Tx(decimal amount, TransactionType type, DateOnly date, TransactionStatus status,
                ExpenseNature nature = ExpenseNature.Variable, Guid? category = null, Guid? card = null, bool exclude = false)
            => db.Transactions.Add(new Transaction
            {
                UserId = UserId,
                Description = "Mov",
                Amount = amount,
                Type = type,
                Status = status,
                Nature = nature,
                TransactionDate = date,
                CategoryId = category,
                AccountId = card == null ? account.Id : null,
                CreditCardId = card,
                ExcludeFromReports = exclude
            });

        // Maio/2026
        Tx(4000m, TransactionType.Income, new(2026, 5, 5), TransactionStatus.Confirmed);
        Tx(1000m, TransactionType.Expense, new(2026, 5, 10), TransactionStatus.Confirmed, ExpenseNature.Fixed, RentId);

        // Junho/2026
        Tx(5000m, TransactionType.Income, new(2026, 6, 5), TransactionStatus.Confirmed);
        Tx(1500m, TransactionType.Expense, new(2026, 6, 10), TransactionStatus.Confirmed, ExpenseNature.Fixed, RentId);
        Tx(300m, TransactionType.Expense, new(2026, 6, 12), TransactionStatus.Confirmed, ExpenseNature.Variable, FoodId);
        Tx(200m, TransactionType.Expense, new(2026, 6, 8), TransactionStatus.Confirmed, ExpenseNature.Variable, FoodId, card: CardId);
        Tx(100m, TransactionType.Expense, new(2026, 6, 20), TransactionStatus.Forecast, ExpenseNature.Variable, FoodId); // previsto conta no relatório
        Tx(400m, TransactionType.Transfer, new(2026, 6, 6), TransactionStatus.Confirmed, exclude: true);                // excluída
        Tx(999m, TransactionType.Expense, new(2026, 6, 15), TransactionStatus.Canceled, ExpenseNature.Variable, FoodId); // cancelada

        db.SaveChanges();
        return new ReportService(db, new FakeCurrentUser(UserId));
    }

    [Fact]
    public async Task Monthly_series_has_requested_window_with_zero_months()
    {
        var report = await Build().GetSummaryAsync(2026, 6, months: 3);

        report.MonthlySeries.Should().HaveCount(3);
        var apr = report.MonthlySeries[0];
        apr.Month.Should().Be(4);
        apr.Income.Should().Be(0m);
        apr.Expense.Should().Be(0m);

        var may = report.MonthlySeries[1];
        may.Income.Should().Be(4000m);
        may.Expense.Should().Be(1000m);
        may.Result.Should().Be(3000m);

        var jun = report.MonthlySeries[2];
        jun.Income.Should().Be(5000m);
        jun.Expense.Should().Be(2100m);
        jun.Label.Should().Be("jun/26");
    }

    [Fact]
    public async Task Month_totals_include_forecast_and_ignore_transfers_and_canceled()
    {
        var report = await Build().GetSummaryAsync(2026, 6, months: 3);

        report.MonthIncome.Should().Be(5000m);
        report.MonthExpense.Should().Be(2100m);  // 1500 + 300 + 200 + 100 (previsto); 400 transf. e 999 cancelada fora
        report.MonthResult.Should().Be(2900m);
    }

    [Fact]
    public async Task Fixed_vs_variable_split_by_nature()
    {
        var report = await Build().GetSummaryAsync(2026, 6, months: 3);

        report.FixedVsVariable.Fixed.Should().Be(1500m);
        report.FixedVsVariable.Variable.Should().Be(600m);  // 300 + 200 + 100
        report.FixedVsVariable.FixedPercent.Should().Be(71.4m);
        report.FixedVsVariable.VariablePercent.Should().Be(28.6m);
    }

    [Fact]
    public async Task Expenses_by_category_ordered_desc_with_percent()
    {
        var report = await Build().GetSummaryAsync(2026, 6, months: 3);

        report.ExpensesByCategory.Should().HaveCount(2);
        report.ExpensesByCategory[0].CategoryName.Should().Be("Moradia");
        report.ExpensesByCategory[0].Amount.Should().Be(1500m);
        report.ExpensesByCategory[0].Percent.Should().Be(71.4m);
        report.ExpensesByCategory[1].CategoryName.Should().Be("Alimentação");
        report.ExpensesByCategory[1].Amount.Should().Be(600m);
    }
}
