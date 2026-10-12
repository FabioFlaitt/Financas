using FinanceApp.Application.Imports;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class StatementImportServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (StatementImportService service, AppDbContext db, Guid accountId) Build()
    {
        var db = TestDb.NewContext();
        var account = new Account { Id = Guid.NewGuid(), UserId = UserId, Name = "Corrente", InitialBalance = 0m };
        db.Accounts.Add(account);
        db.SaveChanges();
        return (new StatementImportService(db, new FakeCurrentUser(UserId)), db, account.Id);
    }

    private const string Ofx = @"<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
<STMTTRN>
<TRNTYPE>DEBIT
<DTPOSTED>20260605120000
<TRNAMT>-50.00
<FITID>ABC123
<NAME>Mercado
</STMTTRN>
<STMTTRN>
<TRNTYPE>CREDIT
<DTPOSTED>20260601
<TRNAMT>5000.00
<FITID>SAL001
<MEMO>Salario
</STMTTRN>
</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";

    private const string Csv = "Data;Descricao;Valor\n05/06/2026;Padaria;-25,90\n01/06/2026;Salario;5.000,00\n";

    [Fact]
    public async Task Ofx_preview_parses_transactions_with_type_by_sign()
    {
        var (service, _, accountId) = Build();

        var result = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Ofx, Content = Ofx });

        result.TotalParsed.Should().Be(2);
        result.Duplicates.Should().Be(0);

        var debit = result.Items.Single(i => i.Description == "Mercado");
        debit.Type.Should().Be(TransactionType.Expense);
        debit.Amount.Should().Be(50m);
        debit.Date.Should().Be(new DateOnly(2026, 6, 5));
        debit.ExternalId.Should().Contain("ABC123");

        var credit = result.Items.Single(i => i.Description == "Salario");
        credit.Type.Should().Be(TransactionType.Income);
        credit.Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task Csv_preview_handles_ptbr_amounts_and_delimiter()
    {
        var (service, _, accountId) = Build();

        var result = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Csv, Content = Csv });

        result.TotalParsed.Should().Be(2);
        var padaria = result.Items.Single(i => i.Description == "Padaria");
        padaria.Type.Should().Be(TransactionType.Expense);
        padaria.Amount.Should().Be(25.90m);
        padaria.Date.Should().Be(new DateOnly(2026, 6, 5));

        result.Items.Single(i => i.Description == "Salario").Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task Preview_flags_already_imported_items_as_duplicate()
    {
        var (service, db, accountId) = Build();
        var first = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Ofx, Content = Ofx });

        // Simula que um dos itens já foi importado antes.
        var existingId = first.Items.First().ExternalId;
        db.Transactions.Add(new Transaction
        {
            UserId = UserId, Description = "Já importado", Amount = 1m, Type = TransactionType.Expense,
            Status = TransactionStatus.Confirmed, TransactionDate = new DateOnly(2026, 6, 5),
            AccountId = accountId, Origin = TransactionOrigin.ImportedFile, ExternalId = existingId
        });
        db.SaveChanges();

        var second = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Ofx, Content = Ofx });

        second.Duplicates.Should().Be(1);
        second.Items.Single(i => i.ExternalId == existingId).IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task Commit_is_idempotent()
    {
        var (service, db, accountId) = Build();
        var preview = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Ofx, Content = Ofx });

        var firstCommit = await service.CommitAsync(new ImportCommitRequest { AccountId = accountId, Items = preview.Items });
        firstCommit.Imported.Should().Be(2);
        firstCommit.SkippedDuplicates.Should().Be(0);

        var secondCommit = await service.CommitAsync(new ImportCommitRequest { AccountId = accountId, Items = preview.Items });
        secondCommit.Imported.Should().Be(0);
        secondCommit.SkippedDuplicates.Should().Be(2);

        db.Transactions.Count(t => t.Origin == TransactionOrigin.ImportedFile).Should().Be(2);
    }

    [Fact]
    public async Task Commit_within_a_single_batch_dedupes_repeated_ids()
    {
        var (service, db, accountId) = Build();
        var preview = await service.PreviewAsync(new ImportPreviewRequest
        { AccountId = accountId, Format = ImportFormat.Ofx, Content = Ofx });

        // Mesmo item repetido no lote deve ser importado apenas uma vez.
        var items = preview.Items.Concat(preview.Items).ToList();
        var result = await service.CommitAsync(new ImportCommitRequest { AccountId = accountId, Items = items });

        result.Imported.Should().Be(2);
        result.SkippedDuplicates.Should().Be(2);
        db.Transactions.Count().Should().Be(2);
    }
}
