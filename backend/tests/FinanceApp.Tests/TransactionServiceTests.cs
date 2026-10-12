using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class TransactionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (TransactionService service, AppDbContext db, Guid accountId, Guid destId, Guid categoryId) Build()
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 0 };
        var dest = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Poupança", InitialBalance = 0 };
        var category = new Category { Id = Guid.NewGuid(), UserId = UserId, Name = "Alimentação" };
        db.Accounts.AddRange(account, dest);
        db.Categories.Add(category);
        db.SaveChanges();

        var service = new TransactionService(db, new FakeCurrentUser(UserId));
        return (service, db, account.Id, dest.Id, category.Id);
    }

    [Fact]
    public async Task Create_persists_expense_with_labels()
    {
        var (service, _, accountId, _, categoryId) = Build();

        var dto = await service.CreateAsync(new CreateTransactionRequest
        {
            Description = "Mercado",
            Amount = 123.45m,
            Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 10),
            AccountId = accountId,
            CategoryId = categoryId
        });

        dto.Id.Should().NotBeEmpty();
        dto.TypeLabel.Should().Be("Despesa");
        dto.StatusLabel.Should().Be("Confirmado");
        dto.CategoryName.Should().Be("Alimentação");
        dto.AccountName.Should().Be("Corrente");
    }

    [Fact]
    public async Task Create_rejects_non_positive_amount()
    {
        var (service, _, accountId, _, _) = Build();

        var act = () => service.CreateAsync(new CreateTransactionRequest
        {
            Description = "Zero",
            Amount = 0m,
            Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 10),
            AccountId = accountId
        });

        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Transfer_requires_distinct_source_and_destination()
    {
        var (service, _, accountId, _, _) = Build();

        var act = () => service.CreateAsync(new CreateTransactionRequest
        {
            Description = "Transfência inválida",
            Amount = 100m,
            Type = TransactionType.Transfer,
            TransactionDate = new DateOnly(2026, 6, 10),
            AccountId = accountId,
            DestinationAccountId = accountId
        });

        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Transfer_is_excluded_from_reports()
    {
        var (service, _, accountId, destId, _) = Build();

        var dto = await service.CreateAsync(new CreateTransactionRequest
        {
            Description = "Transferência",
            Amount = 100m,
            Type = TransactionType.Transfer,
            TransactionDate = new DateOnly(2026, 6, 10),
            AccountId = accountId,
            DestinationAccountId = destId
        });

        var reloaded = await service.GetByIdAsync(dto.Id);
        reloaded.Type.Should().Be(TransactionType.Transfer);
    }

    [Fact]
    public async Task Duplicate_creates_independent_copy()
    {
        var (service, db, accountId, _, categoryId) = Build();
        var original = await service.CreateAsync(new CreateTransactionRequest
        {
            Description = "Assinatura",
            Amount = 29.90m,
            Type = TransactionType.Expense,
            TransactionDate = new DateOnly(2026, 6, 1),
            AccountId = accountId,
            CategoryId = categoryId
        });

        var copy = await service.DuplicateAsync(original.Id);

        copy.Id.Should().NotBe(original.Id);
        db.Transactions.Count().Should().Be(2);
    }

    [Fact]
    public async Task List_filters_by_type_and_search()
    {
        var (service, _, accountId, _, categoryId) = Build();
        await service.CreateAsync(new CreateTransactionRequest { Description = "Salário", Amount = 5000m, Type = TransactionType.Income, TransactionDate = new DateOnly(2026, 6, 5), AccountId = accountId });
        await service.CreateAsync(new CreateTransactionRequest { Description = "Mercado Extra", Amount = 200m, Type = TransactionType.Expense, TransactionDate = new DateOnly(2026, 6, 6), AccountId = accountId, CategoryId = categoryId });

        var expenses = await service.ListAsync(new TransactionListQuery { Type = TransactionType.Expense });
        expenses.TotalItems.Should().Be(1);

        var search = await service.ListAsync(new TransactionListQuery { Search = "mercado" });
        search.TotalItems.Should().Be(1);
        search.Items[0].Description.Should().Be("Mercado Extra");
    }

    [Fact]
    public async Task Delete_removes_transaction()
    {
        var (service, db, accountId, _, _) = Build();
        var dto = await service.CreateAsync(new CreateTransactionRequest { Description = "X", Amount = 10m, Type = TransactionType.Expense, TransactionDate = new DateOnly(2026, 6, 6), AccountId = accountId });

        await service.DeleteAsync(dto.Id);

        db.Transactions.Count().Should().Be(0);
    }
}
