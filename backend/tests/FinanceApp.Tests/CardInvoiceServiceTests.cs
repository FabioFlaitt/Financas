using FinanceApp.Application.Cards;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class CardInvoiceServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 5);

    private sealed record Ctx(
        InvoiceService Invoices, CardService Cards, TransactionService Transactions,
        AppDbContext Db, Guid CardId, Guid AccountId, Guid CategoryId);

    private static Ctx Build()
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 5000m };
        var category = new Category { Id = Guid.NewGuid(), UserId = UserId, Name = "Compras", ColorHex = "#5E35B1" };
        var card = new CreditCard
        {
            Id = Guid.NewGuid(), UserId = UserId, Name = "Nubank",
            ClosingDay = 10, DueDay = 20, CreditLimit = 1000m, DefaultPaymentAccountId = account.Id
        };
        db.Accounts.Add(account);
        db.Categories.Add(category);
        db.CreditCards.Add(card);
        db.SaveChanges();

        var currentUser = new FakeCurrentUser(UserId);
        var clock = new FakeClock(Today);
        var invoices = new InvoiceService(db, currentUser, clock);
        var cards = new CardService(db, currentUser, clock);
        var transactions = new TransactionService(db, currentUser, invoices);
        return new Ctx(invoices, cards, transactions, db, card.Id, account.Id, category.Id);
    }

    [Fact]
    public void ResolveCycle_rolls_to_next_month_after_closing_day()
    {
        var before = InvoiceService.ResolveCycle(10, 20, new DateOnly(2026, 6, 5));
        before.referenceMonth.Should().Be(new DateOnly(2026, 6, 1));
        before.closingDate.Should().Be(new DateOnly(2026, 6, 10));
        before.dueDate.Should().Be(new DateOnly(2026, 6, 20));

        var after = InvoiceService.ResolveCycle(10, 20, new DateOnly(2026, 6, 15));
        after.referenceMonth.Should().Be(new DateOnly(2026, 7, 1));
        after.closingDate.Should().Be(new DateOnly(2026, 7, 10));
        after.dueDate.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public async Task Card_purchase_is_allocated_to_the_cycle_invoice()
    {
        var ctx = Build();

        await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra A", Amount = 100m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 5), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });
        await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra B", Amount = 50m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 15), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });

        var invoices = (await ctx.Invoices.ListByCardAsync(ctx.CardId)).OrderBy(i => i.ReferenceMonth).ToList();
        invoices.Should().HaveCount(2);
        invoices[0].ReferenceMonth.Should().Be(new DateOnly(2026, 6, 1));
        invoices[0].TotalAmount.Should().Be(100m);
        invoices[1].ReferenceMonth.Should().Be(new DateOnly(2026, 7, 1));
        invoices[1].TotalAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Installment_splits_across_invoices_and_closes_total_exactly()
    {
        var ctx = Build();

        await ctx.Invoices.AddInstallmentAsync(new AddInstallmentRequest
        {
            CreditCardId = ctx.CardId, Description = "Notebook", TotalAmount = 100m,
            NumberOfInstallments = 3, PurchaseDate = new DateOnly(2026, 6, 3), CategoryId = ctx.CategoryId
        });

        ctx.Db.Transactions.Count(t => t.InstallmentId != null).Should().Be(3);
        ctx.Db.Transactions.Where(t => t.InstallmentId != null).Sum(t => t.Amount).Should().Be(100m);

        var invoices = (await ctx.Invoices.ListByCardAsync(ctx.CardId)).OrderBy(i => i.ReferenceMonth).ToList();
        invoices.Should().HaveCount(3);
        invoices[0].TotalAmount.Should().Be(33.33m);               // junho
        invoices[2].TotalAmount.Should().Be(33.34m);               // agosto (absorve o arredondamento)
    }

    [Fact]
    public async Task Pay_full_marks_invoice_paid_and_records_payment()
    {
        var ctx = Build();
        await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra", Amount = 200m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 5), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });
        var invoice = (await ctx.Invoices.ListByCardAsync(ctx.CardId)).Single();

        var result = await ctx.Invoices.PayAsync(invoice.Id, new PayInvoiceRequest { AccountId = ctx.AccountId });

        result.Status.Should().Be(InvoiceStatus.Paid);
        result.PaidAmount.Should().Be(200m);
        result.OutstandingAmount.Should().Be(0m);
        ctx.Db.Transactions.Count(t => t.Type == TransactionType.CreditCardPayment && t.ExcludeFromReports).Should().Be(1);
    }

    [Fact]
    public async Task Partial_payment_keeps_invoice_open()
    {
        var ctx = Build();
        await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra", Amount = 200m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 5), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });
        var invoice = (await ctx.Invoices.ListByCardAsync(ctx.CardId)).Single();

        var result = await ctx.Invoices.PayAsync(invoice.Id, new PayInvoiceRequest { AccountId = ctx.AccountId, Amount = 50m });

        result.Status.Should().Be(InvoiceStatus.Open);
        result.PaidAmount.Should().Be(50m);
        result.OutstandingAmount.Should().Be(150m);
    }

    [Fact]
    public async Task Card_list_computes_open_total_and_available_limit()
    {
        var ctx = Build();
        await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra", Amount = 200m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 3), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });

        var card = (await ctx.Cards.ListAsync()).Single();
        card.OpenTotal.Should().Be(200m);
        card.AvailableLimit.Should().Be(800m);
        card.UsedLimitPercent.Should().Be(20m);
        card.CurrentInvoiceTotal.Should().Be(200m); // ciclo atual (hoje 05/06, fecha dia 10) = junho
    }

    [Fact]
    public async Task Deleting_a_card_purchase_updates_the_invoice_total()
    {
        var ctx = Build();
        var tx = await ctx.Transactions.CreateAsync(new CreateTransactionRequest
        {
            Description = "Compra", Amount = 80m, Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 5), CreditCardId = ctx.CardId, CategoryId = ctx.CategoryId
        });

        await ctx.Transactions.DeleteAsync(tx.Id);

        var invoice = (await ctx.Invoices.ListByCardAsync(ctx.CardId)).Single();
        invoice.TotalAmount.Should().Be(0m);
    }
}
