using FinanceApp.Application.Recurrences;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class RecurringTransactionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static RecurringTransactionService Build(DateOnly today, out FinanceApp.Infrastructure.Persistence.AppDbContext db)
    {
        db = TestDb.NewContext();
        return new RecurringTransactionService(db, new FakeCurrentUser(UserId), new FakeClock(today));
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Monthly, "2026-01-31", "2026-02-28")]
    [InlineData(RecurrenceFrequency.Weekly, "2026-06-01", "2026-06-08")]
    [InlineData(RecurrenceFrequency.Yearly, "2026-06-01", "2027-06-01")]
    public void NextDate_advances_by_frequency(RecurrenceFrequency freq, string from, string expected)
    {
        var result = RecurringTransactionService.NextDate(DateOnly.Parse(from), freq);
        result.Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public async Task GenerateDue_materializes_occurrences_up_to_today()
    {
        var svc = Build(new DateOnly(2026, 4, 15), out var db);
        await svc.CreateAsync(new SaveRecurringTransactionRequest
        {
            Description = "Aluguel",
            Amount = 2000m,
            Type = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 10)
        });

        var generated = await svc.GenerateDueAsync();

        // Jan, Fev, Mar, Abr (dia 10) => 4 lançamentos
        generated.Should().Be(4);
        db.Transactions.Count(t => t.UserId == UserId).Should().Be(4);
    }

    [Fact]
    public async Task GenerateDue_is_idempotent()
    {
        var svc = Build(new DateOnly(2026, 3, 20), out var db);
        await svc.CreateAsync(new SaveRecurringTransactionRequest
        {
            Description = "Internet",
            Amount = 120m,
            Type = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 5)
        });

        var first = await svc.GenerateDueAsync();
        var second = await svc.GenerateDueAsync();

        first.Should().Be(3);   // Jan, Fev, Mar
        second.Should().Be(0);  // nada novo na segunda execução
        db.Transactions.Count(t => t.UserId == UserId).Should().Be(3);
    }

    [Fact]
    public async Task GenerateDue_respects_end_date()
    {
        var svc = Build(new DateOnly(2026, 12, 31), out var db);
        await svc.CreateAsync(new SaveRecurringTransactionRequest
        {
            Description = "Curso",
            Amount = 300m,
            Type = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 3, 1)
        });

        var generated = await svc.GenerateDueAsync();
        generated.Should().Be(3); // Jan, Fev, Mar apenas
    }
}
