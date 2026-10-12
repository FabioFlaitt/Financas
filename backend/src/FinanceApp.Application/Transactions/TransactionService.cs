using FinanceApp.Application.Cards;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Application.Common.Models;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> ListAsync(TransactionListQuery query, CancellationToken ct = default);
    Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TransactionDto> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> UpdateAsync(UpdateTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> DuplicateAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IInvoiceService _invoices;

    public TransactionService(IApplicationDbContext db, ICurrentUser currentUser, IInvoiceService invoices)
    {
        _db = db;
        _currentUser = currentUser;
        _invoices = invoices;
    }

    private static bool IsCardPurchase(TransactionType type, Guid? creditCardId)
        => creditCardId.HasValue && type == TransactionType.Expense;

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<PagedResult<TransactionDto>> ListAsync(TransactionListQuery query, CancellationToken ct = default)
    {
        var q = _db.Transactions.Where(t => t.UserId == UserId);

        if (query.Year is int y && query.Month is int m)
        {
            var start = new DateOnly(y, m, 1);
            var end = start.AddMonths(1).AddDays(-1);
            q = q.Where(t => t.TransactionDate >= start && t.TransactionDate <= end);
        }
        if (query.From is DateOnly from) q = q.Where(t => t.TransactionDate >= from);
        if (query.To is DateOnly to) q = q.Where(t => t.TransactionDate <= to);
        if (query.CategoryId is Guid cat) q = q.Where(t => t.CategoryId == cat);
        if (query.AccountId is Guid acc) q = q.Where(t => t.AccountId == acc || t.DestinationAccountId == acc);
        if (query.CreditCardId is Guid card) q = q.Where(t => t.CreditCardId == card);
        if (query.Type is TransactionType type) q = q.Where(t => t.Type == type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(t => t.Description.ToLower().Contains(s) ||
                             (t.Merchant != null && t.Merchant.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(t => t.Category)
            .Include(t => t.Account)
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>
        {
            Items = items.Select(Map).ToList(),
            Page = page,
            PageSize = size,
            TotalItems = total
        };
    }

    public async Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tx = await _db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId, ct)
            ?? throw NotFoundException.For("Lançamento", id);
        return Map(tx);
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default)
    {
        await ValidateReferencesAsync(request, ct);

        var tx = new Transaction
        {
            UserId = UserId,
            Description = request.Description.Trim(),
            Amount = Math.Abs(request.Amount),
            Type = request.Type,
            Status = request.Status,
            TransactionDate = request.TransactionDate,
            DueDate = request.DueDate,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            DestinationAccountId = request.DestinationAccountId,
            CreditCardId = request.CreditCardId,
            Merchant = request.Merchant?.Trim(),
            PaymentMethod = request.PaymentMethod,
            Nature = request.Nature,
            Recurrence = request.Recurrence,
            Notes = request.Notes,
            Origin = TransactionOrigin.Manual,
            // Transferências e pagamentos de fatura não contam como despesa/receita real nos relatórios.
            ExcludeFromReports = request.Type is TransactionType.Transfer or TransactionType.CreditCardPayment
        };

        // Compra no cartão: aloca na fatura do ciclo correspondente.
        if (IsCardPurchase(tx.Type, tx.CreditCardId))
            tx.InvoiceId = await _invoices.EnsureInvoiceIdAsync(tx.CreditCardId!.Value, tx.TransactionDate, ct);

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        if (tx.InvoiceId is Guid invoiceId)
            await _invoices.RecalculateAsync(invoiceId, ct);

        return await GetByIdAsync(tx.Id, ct);
    }

    public async Task<TransactionDto> UpdateAsync(UpdateTransactionRequest request, CancellationToken ct = default)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == UserId, ct)
            ?? throw NotFoundException.For("Lançamento", request.Id);

        await ValidateReferencesAsync(request, ct);

        var oldInvoiceId = tx.InvoiceId;

        tx.Description = request.Description.Trim();
        tx.Amount = Math.Abs(request.Amount);
        tx.Type = request.Type;
        tx.Status = request.Status;
        tx.TransactionDate = request.TransactionDate;
        tx.DueDate = request.DueDate;
        tx.CategoryId = request.CategoryId;
        tx.AccountId = request.AccountId;
        tx.DestinationAccountId = request.DestinationAccountId;
        tx.CreditCardId = request.CreditCardId;
        tx.Merchant = request.Merchant?.Trim();
        tx.PaymentMethod = request.PaymentMethod;
        tx.Nature = request.Nature;
        tx.Recurrence = request.Recurrence;
        tx.Notes = request.Notes;
        tx.ExcludeFromReports = request.Type is TransactionType.Transfer or TransactionType.CreditCardPayment;
        tx.UpdatedAtUtc = DateTime.UtcNow;

        // Realoca a fatura conforme o novo cartão/data (ou desvincula se deixou de ser compra no cartão).
        tx.InvoiceId = IsCardPurchase(tx.Type, tx.CreditCardId)
            ? await _invoices.EnsureInvoiceIdAsync(tx.CreditCardId!.Value, tx.TransactionDate, ct)
            : null;

        await _db.SaveChangesAsync(ct);

        foreach (var invId in new[] { oldInvoiceId, tx.InvoiceId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct())
            await _invoices.RecalculateAsync(invId, ct);

        return await GetByIdAsync(tx.Id, ct);
    }

    public async Task<TransactionDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var src = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId, ct)
            ?? throw NotFoundException.For("Lançamento", id);

        var copy = new Transaction
        {
            UserId = UserId,
            Description = src.Description,
            Amount = src.Amount,
            Type = src.Type,
            Status = src.Status,
            TransactionDate = src.TransactionDate,
            DueDate = src.DueDate,
            CategoryId = src.CategoryId,
            AccountId = src.AccountId,
            DestinationAccountId = src.DestinationAccountId,
            CreditCardId = src.CreditCardId,
            Merchant = src.Merchant,
            PaymentMethod = src.PaymentMethod,
            Nature = src.Nature,
            Recurrence = RecurrenceFrequency.None,
            Notes = src.Notes,
            Origin = TransactionOrigin.Manual,
            ExcludeFromReports = src.ExcludeFromReports
        };

        if (IsCardPurchase(copy.Type, copy.CreditCardId))
            copy.InvoiceId = await _invoices.EnsureInvoiceIdAsync(copy.CreditCardId!.Value, copy.TransactionDate, ct);

        _db.Transactions.Add(copy);
        await _db.SaveChangesAsync(ct);

        if (copy.InvoiceId is Guid invoiceId)
            await _invoices.RecalculateAsync(invoiceId, ct);

        return await GetByIdAsync(copy.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId, ct)
            ?? throw NotFoundException.For("Lançamento", id);
        var invoiceId = tx.InvoiceId;
        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync(ct);

        if (invoiceId is Guid invId)
            await _invoices.RecalculateAsync(invId, ct);
    }

    private async Task ValidateReferencesAsync(CreateTransactionRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Description))
            throw new AppValidationException(nameof(r.Description), "A descrição é obrigatória.");
        if (r.Amount <= 0)
            throw new AppValidationException(nameof(r.Amount), "O valor deve ser maior que zero.");
        if (r.Type == TransactionType.Transfer && (r.AccountId == null || r.DestinationAccountId == null))
            throw new AppValidationException(nameof(r.DestinationAccountId), "Transferências exigem conta de origem e destino.");
        if (r.Type == TransactionType.Transfer && r.AccountId == r.DestinationAccountId)
            throw new AppValidationException(nameof(r.DestinationAccountId), "A conta de destino deve ser diferente da origem.");

        if (r.AccountId is Guid accId &&
            !await _db.Accounts.AnyAsync(a => a.Id == accId && a.UserId == UserId, ct))
            throw new AppValidationException(nameof(r.AccountId), "Conta inválida.");
        if (r.CategoryId is Guid catId &&
            !await _db.Categories.AnyAsync(c => c.Id == catId && c.UserId == UserId, ct))
            throw new AppValidationException(nameof(r.CategoryId), "Categoria inválida.");
        if (r.CreditCardId is Guid cardId &&
            !await _db.CreditCards.AnyAsync(c => c.Id == cardId && c.UserId == UserId, ct))
            throw new AppValidationException(nameof(r.CreditCardId), "Cartão inválido.");
    }

    private static TransactionDto Map(Transaction t) => new()
    {
        Id = t.Id,
        Description = t.Description,
        Amount = t.Amount,
        Type = t.Type,
        TypeLabel = t.Type.GetDescription(),
        Status = t.Status,
        StatusLabel = t.Status.GetDescription(),
        TransactionDate = t.TransactionDate,
        DueDate = t.DueDate,
        CategoryId = t.CategoryId,
        CategoryName = t.Category?.Name,
        CategoryColorHex = t.Category?.ColorHex,
        AccountId = t.AccountId,
        AccountName = t.Account?.Name,
        DestinationAccountId = t.DestinationAccountId,
        CreditCardId = t.CreditCardId,
        Merchant = t.Merchant,
        PaymentMethod = t.PaymentMethod,
        Nature = t.Nature,
        Origin = t.Origin,
        Recurrence = t.Recurrence,
        Notes = t.Notes
    };
}
