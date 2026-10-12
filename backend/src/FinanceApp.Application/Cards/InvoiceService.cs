using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Cards;

public record InvoiceListItemDto
{
    public Guid Id { get; init; }
    public Guid CreditCardId { get; init; }
    public DateOnly ReferenceMonth { get; init; }
    public DateOnly ClosingDate { get; init; }
    public DateOnly DueDate { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public bool IsOverdue { get; init; }
}

public record InvoiceTransactionDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateOnly TransactionDate { get; init; }
    public string? CategoryName { get; init; }
    public string? CategoryColorHex { get; init; }
    public bool IsInstallment { get; init; }
}

public record InvoiceDto : InvoiceListItemDto
{
    public string CardName { get; init; } = string.Empty;
    public IReadOnlyList<InvoiceTransactionDto> Transactions { get; init; } = new List<InvoiceTransactionDto>();
}

public record PayInvoiceRequest
{
    public Guid AccountId { get; init; }
    /// <summary>Valor a pagar; nulo paga todo o saldo em aberto.</summary>
    public decimal? Amount { get; init; }
    public DateOnly? Date { get; init; }
}

public record AddInstallmentRequest
{
    public Guid CreditCardId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int NumberOfInstallments { get; init; }
    public DateOnly PurchaseDate { get; init; }
    public Guid? CategoryId { get; init; }
}

public interface IInvoiceService
{
    /// <summary>Garante a fatura do ciclo que contém a data da compra e devolve seu Id.</summary>
    Task<Guid> EnsureInvoiceIdAsync(Guid creditCardId, DateOnly purchaseDate, CancellationToken ct = default);

    /// <summary>Recalcula o total de uma fatura a partir das compras alocadas e ajusta o status.</summary>
    Task RecalculateAsync(Guid invoiceId, CancellationToken ct = default);

    Task<IReadOnlyList<InvoiceListItemDto>> ListByCardAsync(Guid creditCardId, CancellationToken ct = default);
    Task<InvoiceDto> GetAsync(Guid invoiceId, CancellationToken ct = default);
    Task<InvoiceListItemDto> CloseAsync(Guid invoiceId, CancellationToken ct = default);
    Task<InvoiceListItemDto> PayAsync(Guid invoiceId, PayInvoiceRequest request, CancellationToken ct = default);
    Task AddInstallmentAsync(AddInstallmentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Regras de fechamento/pagamento das faturas de cartão. Uma compra pertence à fatura do ciclo
/// que fecha no primeiro <c>ClosingDay</c> em ou após a data da compra: compras feitas até o dia
/// de fechamento entram na fatura do mês corrente; depois, na do mês seguinte.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public InvoiceService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    private static DateOnly ClampDay(int year, int month, int day)
        => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    /// <summary>Calcula o ciclo (mês de referência, fechamento e vencimento) de uma compra.</summary>
    public static (DateOnly referenceMonth, DateOnly closingDate, DateOnly dueDate) ResolveCycle(
        int closingDay, int dueDay, DateOnly purchaseDate)
    {
        var closingThisMonth = ClampDay(purchaseDate.Year, purchaseDate.Month, closingDay);
        var refMonth = purchaseDate <= closingThisMonth
            ? new DateOnly(purchaseDate.Year, purchaseDate.Month, 1)
            : new DateOnly(purchaseDate.Year, purchaseDate.Month, 1).AddMonths(1);

        var closingDate = ClampDay(refMonth.Year, refMonth.Month, closingDay);
        // Vencimento costuma cair alguns dias após o fechamento; se o dia de vencimento é menor ou
        // igual ao de fechamento, ele ocorre no mês seguinte ao do fechamento.
        var dueMonth = dueDay > closingDay ? refMonth : refMonth.AddMonths(1);
        var dueDate = ClampDay(dueMonth.Year, dueMonth.Month, dueDay);
        return (refMonth, closingDate, dueDate);
    }

    public async Task<Guid> EnsureInvoiceIdAsync(Guid creditCardId, DateOnly purchaseDate, CancellationToken ct = default)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == creditCardId && c.UserId == UserId, ct)
            ?? throw new AppValidationException(nameof(creditCardId), "Cartão inválido.");

        var (refMonth, closingDate, dueDate) = ResolveCycle(card.ClosingDay, card.DueDay, purchaseDate);

        var invoice = await _db.CreditCardInvoices.FirstOrDefaultAsync(
            i => i.UserId == UserId && i.CreditCardId == creditCardId && i.ReferenceMonth == refMonth, ct);

        if (invoice is null)
        {
            invoice = new CreditCardInvoice
            {
                UserId = UserId,
                CreditCardId = creditCardId,
                ReferenceMonth = refMonth,
                ClosingDate = closingDate,
                DueDate = dueDate,
                Status = InvoiceStatus.Open
            };
            _db.CreditCardInvoices.Add(invoice);
            await _db.SaveChangesAsync(ct);
        }
        return invoice.Id;
    }

    public async Task RecalculateAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.CreditCardInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == UserId, ct);
        if (invoice is null) return;

        invoice.TotalAmount = await _db.Transactions
            .Where(t => t.InvoiceId == invoiceId &&
                        t.Type == TransactionType.Expense &&
                        t.Status != TransactionStatus.Canceled)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        if (invoice.Status == InvoiceStatus.Paid && invoice.PaidAmount < invoice.TotalAmount)
            invoice.Status = InvoiceStatus.Open; // uma nova compra reabriu a fatura já quitada
        invoice.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> ListByCardAsync(Guid creditCardId, CancellationToken ct = default)
    {
        _ = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == creditCardId && c.UserId == UserId, ct)
            ?? throw NotFoundException.For("Cartão", creditCardId);

        var invoices = await _db.CreditCardInvoices
            .Where(i => i.UserId == UserId && i.CreditCardId == creditCardId)
            .OrderByDescending(i => i.ReferenceMonth)
            .ToListAsync(ct);

        return invoices.Select(MapListItem).ToList();
    }

    public async Task<InvoiceDto> GetAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.CreditCardInvoices
            .Include(i => i.CreditCard)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == UserId, ct)
            ?? throw NotFoundException.For("Fatura", invoiceId);

        var items = await _db.Transactions
            .Where(t => t.InvoiceId == invoiceId && t.Type == TransactionType.Expense && t.Status != TransactionStatus.Canceled)
            .Include(t => t.Category)
            .OrderBy(t => t.TransactionDate)
            .Select(t => new InvoiceTransactionDto
            {
                Id = t.Id,
                Description = t.Description,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                CategoryName = t.Category != null ? t.Category.Name : null,
                CategoryColorHex = t.Category != null ? t.Category.ColorHex : null,
                IsInstallment = t.InstallmentId != null
            })
            .ToListAsync(ct);

        var baseDto = MapListItem(invoice);
        return new InvoiceDto
        {
            Id = baseDto.Id,
            CreditCardId = baseDto.CreditCardId,
            ReferenceMonth = baseDto.ReferenceMonth,
            ClosingDate = baseDto.ClosingDate,
            DueDate = baseDto.DueDate,
            TotalAmount = baseDto.TotalAmount,
            PaidAmount = baseDto.PaidAmount,
            OutstandingAmount = baseDto.OutstandingAmount,
            Status = baseDto.Status,
            StatusLabel = baseDto.StatusLabel,
            IsOverdue = baseDto.IsOverdue,
            CardName = invoice.CreditCard?.Name ?? string.Empty,
            Transactions = items
        };
    }

    public async Task<InvoiceListItemDto> CloseAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.CreditCardInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == UserId, ct)
            ?? throw NotFoundException.For("Fatura", invoiceId);

        // Recalcula o total; como o contexto já rastreia esta fatura, a mesma instância é atualizada.
        await RecalculateAsync(invoiceId, ct);

        if (invoice.Status != InvoiceStatus.Paid)
        {
            invoice.Status = InvoiceStatus.Closed;
            invoice.UpdatedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return MapListItem(invoice);
    }

    public async Task<InvoiceListItemDto> PayAsync(Guid invoiceId, PayInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await _db.CreditCardInvoices
            .Include(i => i.CreditCard)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == UserId, ct)
            ?? throw NotFoundException.For("Fatura", invoiceId);

        if (!await _db.Accounts.AnyAsync(a => a.Id == request.AccountId && a.UserId == UserId, ct))
            throw new AppValidationException(nameof(request.AccountId), "Conta de pagamento inválida.");

        var outstanding = invoice.TotalAmount - invoice.PaidAmount;
        var amount = request.Amount ?? outstanding;
        if (amount <= 0)
            throw new AppValidationException(nameof(request.Amount), "O valor do pagamento deve ser maior que zero.");
        if (amount > outstanding + 0.005m)
            throw new AppValidationException(nameof(request.Amount), "O valor é maior que o saldo em aberto da fatura.");

        var date = request.Date ?? _clock.Today;

        // Pagamento de fatura: reduz o caixa da conta, mas não recontabiliza as compras nos relatórios.
        _db.Transactions.Add(new Transaction
        {
            UserId = UserId,
            Description = $"Pagamento fatura {invoice.CreditCard?.Name}".Trim(),
            Amount = amount,
            Type = TransactionType.CreditCardPayment,
            Status = TransactionStatus.Confirmed,
            TransactionDate = date,
            AccountId = request.AccountId,
            CreditCardId = invoice.CreditCardId,
            InvoiceId = invoice.Id,
            PaymentMethod = PaymentMethod.BankTransfer,
            Origin = TransactionOrigin.Manual,
            ExcludeFromReports = true
        });

        invoice.PaidAmount += amount;
        if (invoice.PaidAmount >= invoice.TotalAmount && invoice.TotalAmount > 0)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidDate = date;
        }
        invoice.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapListItem(invoice);
    }

    public async Task AddInstallmentAsync(AddInstallmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new AppValidationException(nameof(request.Description), "A descrição é obrigatória.");
        if (request.TotalAmount <= 0)
            throw new AppValidationException(nameof(request.TotalAmount), "O valor total deve ser maior que zero.");
        if (request.NumberOfInstallments < 1 || request.NumberOfInstallments > 72)
            throw new AppValidationException(nameof(request.NumberOfInstallments), "Número de parcelas inválido.");

        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == request.CreditCardId && c.UserId == UserId, ct)
            ?? throw new AppValidationException(nameof(request.CreditCardId), "Cartão inválido.");
        if (request.CategoryId is Guid catId &&
            !await _db.Categories.AnyAsync(c => c.Id == catId && c.UserId == UserId, ct))
            throw new AppValidationException(nameof(request.CategoryId), "Categoria inválida.");

        var n = request.NumberOfInstallments;
        var per = Math.Round(request.TotalAmount / n, 2, MidpointRounding.AwayFromZero);

        var installment = new Installment
        {
            UserId = UserId,
            CreditCardId = card.Id,
            Description = request.Description.Trim(),
            TotalAmount = request.TotalAmount,
            NumberOfInstallments = n,
            InstallmentAmount = per,
            PurchaseDate = request.PurchaseDate,
            CategoryId = request.CategoryId
        };
        _db.Installments.Add(installment);

        var affected = new HashSet<Guid>();
        decimal accumulated = 0m;
        for (var i = 0; i < n; i++)
        {
            // A última parcela absorve o arredondamento para fechar o total exato.
            var amount = i == n - 1 ? request.TotalAmount - accumulated : per;
            accumulated += per;

            var date = request.PurchaseDate.AddMonths(i);
            var invoiceId = await EnsureInvoiceIdAsync(card.Id, date, ct);
            affected.Add(invoiceId);

            _db.Transactions.Add(new Transaction
            {
                UserId = UserId,
                Description = $"{request.Description.Trim()} ({i + 1}/{n})",
                Amount = amount,
                Type = TransactionType.Expense,
                Status = TransactionStatus.Confirmed,
                TransactionDate = date,
                CategoryId = request.CategoryId,
                CreditCardId = card.Id,
                InvoiceId = invoiceId,
                InstallmentId = installment.Id,
                PaymentMethod = PaymentMethod.Credit,
                Origin = TransactionOrigin.Manual
            });
        }
        await _db.SaveChangesAsync(ct);

        foreach (var id in affected)
            await RecalculateAsync(id, ct);
    }

    private InvoiceListItemDto MapListItem(CreditCardInvoice i)
    {
        var outstanding = i.TotalAmount - i.PaidAmount;
        var isOverdue = i.Status != InvoiceStatus.Paid && outstanding > 0 && _clock.Today > i.DueDate;
        var displayStatus = isOverdue ? InvoiceStatus.Overdue : i.Status;
        return new InvoiceListItemDto
        {
            Id = i.Id,
            CreditCardId = i.CreditCardId,
            ReferenceMonth = i.ReferenceMonth,
            ClosingDate = i.ClosingDate,
            DueDate = i.DueDate,
            TotalAmount = Math.Round(i.TotalAmount, 2),
            PaidAmount = Math.Round(i.PaidAmount, 2),
            OutstandingAmount = Math.Round(outstanding, 2),
            Status = displayStatus,
            StatusLabel = displayStatus.GetDescription(),
            IsOverdue = isOverdue
        };
    }
}
