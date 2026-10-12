using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Cards;

public record CardDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Nickname { get; init; }
    public string? Brand { get; init; }
    public decimal? CreditLimit { get; init; }
    public int ClosingDay { get; init; }
    public int DueDay { get; init; }
    public string ColorHex { get; init; } = "#6A1B9A";
    public string Icon { get; init; } = "credit-card";
    public Guid? DefaultPaymentAccountId { get; init; }

    /// <summary>Total da fatura do ciclo atual.</summary>
    public decimal CurrentInvoiceTotal { get; init; }
    /// <summary>Saldo em aberto somando todas as faturas não pagas.</summary>
    public decimal OpenTotal { get; init; }
    /// <summary>Limite disponível (limite - em aberto), quando há limite informado.</summary>
    public decimal? AvailableLimit { get; init; }
    /// <summary>Percentual do limite comprometido (0-100+), quando há limite.</summary>
    public decimal? UsedLimitPercent { get; init; }
}

public record SaveCardRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Nickname { get; init; }
    public string? Brand { get; init; }
    public decimal? CreditLimit { get; init; }
    public int ClosingDay { get; init; }
    public int DueDay { get; init; }
    public string ColorHex { get; init; } = "#6A1B9A";
    public string Icon { get; init; } = "credit-card";
    public Guid? DefaultPaymentAccountId { get; init; }
}

public interface ICardService
{
    Task<IReadOnlyList<CardDto>> ListAsync(CancellationToken ct = default);
    Task<CardDto> CreateAsync(SaveCardRequest request, CancellationToken ct = default);
    Task<CardDto> UpdateAsync(Guid id, SaveCardRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class CardService : ICardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public CardService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<CardDto>> ListAsync(CancellationToken ct = default)
    {
        var cards = await _db.CreditCards
            .Where(c => c.UserId == UserId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var cardIds = cards.Select(c => c.Id).ToList();
        var invoices = await _db.CreditCardInvoices
            .Where(i => i.UserId == UserId && cardIds.Contains(i.CreditCardId))
            .ToListAsync(ct);
        var invoicesByCard = invoices.ToLookup(i => i.CreditCardId);

        return cards.Select(c => Map(c, invoicesByCard[c.Id])).ToList();
    }

    public async Task<CardDto> CreateAsync(SaveCardRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await ValidatePaymentAccountAsync(request.DefaultPaymentAccountId, ct);

        var card = new CreditCard
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim(),
            Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim(),
            CreditLimit = request.CreditLimit,
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
            ColorHex = request.ColorHex,
            Icon = request.Icon,
            DefaultPaymentAccountId = request.DefaultPaymentAccountId
        };
        _db.CreditCards.Add(card);
        await _db.SaveChangesAsync(ct);
        return Map(card, Enumerable.Empty<CreditCardInvoice>());
    }

    public async Task<CardDto> UpdateAsync(Guid id, SaveCardRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await ValidatePaymentAccountAsync(request.DefaultPaymentAccountId, ct);

        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct)
            ?? throw NotFoundException.For("Cartão", id);

        card.Name = request.Name.Trim();
        card.Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim();
        card.Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        card.CreditLimit = request.CreditLimit;
        card.ClosingDay = request.ClosingDay;
        card.DueDay = request.DueDay;
        card.ColorHex = request.ColorHex;
        card.Icon = request.Icon;
        card.DefaultPaymentAccountId = request.DefaultPaymentAccountId;
        card.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        var invoices = await _db.CreditCardInvoices.Where(i => i.CreditCardId == id && i.UserId == UserId).ToListAsync(ct);
        return Map(card, invoices);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct)
            ?? throw NotFoundException.For("Cartão", id);

        var inUse = await _db.Transactions.AnyAsync(t => t.UserId == UserId && t.CreditCardId == id, ct);
        if (inUse)
        {
            card.IsArchived = true; // preserva o histórico de faturas e compras
            card.UpdatedAtUtc = _clock.UtcNow;
        }
        else
        {
            var invoices = await _db.CreditCardInvoices.Where(i => i.CreditCardId == id && i.UserId == UserId).ToListAsync(ct);
            _db.CreditCardInvoices.RemoveRange(invoices);
            _db.CreditCards.Remove(card);
        }
        await _db.SaveChangesAsync(ct);
    }

    private CardDto Map(CreditCard c, IEnumerable<CreditCardInvoice> invoices)
    {
        var list = invoices.ToList();
        var openTotal = list.Where(i => i.Status != InvoiceStatus.Paid).Sum(i => i.TotalAmount - i.PaidAmount);

        var (currentRef, _, _) = InvoiceService.ResolveCycle(c.ClosingDay, c.DueDay, _clock.Today);
        var currentInvoice = list.FirstOrDefault(i => i.ReferenceMonth == currentRef);

        decimal? available = c.CreditLimit.HasValue ? c.CreditLimit.Value - openTotal : null;
        decimal? usedPercent = c.CreditLimit is > 0 ? Math.Round(openTotal / c.CreditLimit.Value * 100m, 1) : null;

        return new CardDto
        {
            Id = c.Id,
            Name = c.Name,
            Nickname = c.Nickname,
            Brand = c.Brand,
            CreditLimit = c.CreditLimit,
            ClosingDay = c.ClosingDay,
            DueDay = c.DueDay,
            ColorHex = c.ColorHex,
            Icon = c.Icon,
            DefaultPaymentAccountId = c.DefaultPaymentAccountId,
            CurrentInvoiceTotal = Math.Round(currentInvoice?.TotalAmount ?? 0m, 2),
            OpenTotal = Math.Round(openTotal, 2),
            AvailableLimit = available.HasValue ? Math.Round(available.Value, 2) : null,
            UsedLimitPercent = usedPercent
        };
    }

    private static void Validate(SaveCardRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new AppValidationException(nameof(r.Name), "O nome do cartão é obrigatório.");
        if (r.ClosingDay < 1 || r.ClosingDay > 31)
            throw new AppValidationException(nameof(r.ClosingDay), "Dia de fechamento inválido.");
        if (r.DueDay < 1 || r.DueDay > 31)
            throw new AppValidationException(nameof(r.DueDay), "Dia de vencimento inválido.");
        if (r.CreditLimit is < 0)
            throw new AppValidationException(nameof(r.CreditLimit), "O limite não pode ser negativo.");
    }

    private async Task ValidatePaymentAccountAsync(Guid? accountId, CancellationToken ct)
    {
        if (accountId is Guid id &&
            !await _db.Accounts.AnyAsync(a => a.Id == id && a.UserId == UserId, ct))
            throw new AppValidationException(nameof(accountId), "Conta de pagamento inválida.");
    }
}
