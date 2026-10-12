using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Recurrences;

public record RecurringTransactionDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public RecurrenceFrequency Frequency { get; init; }
    public string FrequencyLabel { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateOnly? NextOccurrence { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
}

public record SaveRecurringTransactionRequest
{
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; } = TransactionType.Expense;
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;
    public Guid? CategoryId { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

public interface IRecurringTransactionService
{
    Task<IReadOnlyList<RecurringTransactionDto>> ListAsync(CancellationToken ct = default);
    Task<RecurringTransactionDto> CreateAsync(SaveRecurringTransactionRequest request, CancellationToken ct = default);
    Task<RecurringTransactionDto> UpdateAsync(Guid id, SaveRecurringTransactionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Materializa em lançamentos as ocorrências vencidas até hoje. Retorna quantos foram gerados.</summary>
    Task<int> GenerateDueAsync(CancellationToken ct = default);
}

public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public RecurringTransactionService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<RecurringTransactionDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _db.RecurringTransactions.Where(r => r.UserId == UserId).ToListAsync(ct);
        var categories = await _db.Categories.Where(c => c.UserId == UserId).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return items
            .OrderByDescending(r => r.IsActive).ThenBy(r => r.Description)
            .Select(r => Map(r, categories)).ToList();
    }

    public async Task<RecurringTransactionDto> CreateAsync(SaveRecurringTransactionRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var entity = new RecurringTransaction
        {
            UserId = UserId,
            Description = request.Description.Trim(),
            Amount = Math.Abs(request.Amount),
            Type = request.Type,
            Frequency = request.Frequency,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            CreditCardId = request.CreditCardId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextOccurrence = request.StartDate,
            IsActive = request.IsActive,
            Notes = request.Notes,
            DayOfPeriod = request.StartDate.Day
        };
        _db.RecurringTransactions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return await SingleAsync(entity.Id, ct);
    }

    public async Task<RecurringTransactionDto> UpdateAsync(Guid id, SaveRecurringTransactionRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var entity = await _db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == UserId, ct)
            ?? throw NotFoundException.For("Recorrência", id);

        entity.Description = request.Description.Trim();
        entity.Amount = Math.Abs(request.Amount);
        entity.Type = request.Type;
        entity.Frequency = request.Frequency;
        entity.CategoryId = request.CategoryId;
        entity.AccountId = request.AccountId;
        entity.CreditCardId = request.CreditCardId;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;
        entity.DayOfPeriod = request.StartDate.Day;
        if (entity.NextOccurrence is null || entity.NextOccurrence < request.StartDate)
            entity.NextOccurrence = request.StartDate;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await SingleAsync(entity.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == UserId, ct)
            ?? throw NotFoundException.For("Recorrência", id);
        _db.RecurringTransactions.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GenerateDueAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var recurrences = await _db.RecurringTransactions
            .Where(r => r.UserId == UserId && r.IsActive && r.NextOccurrence != null && r.NextOccurrence <= today)
            .ToListAsync(ct);

        var generated = 0;
        foreach (var r in recurrences)
        {
            var occurrence = r.NextOccurrence!.Value;
            // Gera todas as ocorrências vencidas, respeitando a data final.
            while (occurrence <= today && (r.EndDate is null || occurrence <= r.EndDate) && r.Frequency != RecurrenceFrequency.None)
            {
                var externalId = $"rec:{r.Id}:{occurrence:yyyy-MM-dd}";
                var exists = await _db.Transactions.AnyAsync(t => t.UserId == UserId && t.ExternalId == externalId, ct);
                if (!exists)
                {
                    _db.Transactions.Add(new Transaction
                    {
                        UserId = UserId,
                        Description = r.Description,
                        Amount = r.Amount,
                        Type = r.Type,
                        Status = TransactionStatus.Pending,
                        TransactionDate = occurrence,
                        DueDate = occurrence,
                        CategoryId = r.CategoryId,
                        AccountId = r.AccountId,
                        CreditCardId = r.CreditCardId,
                        Recurrence = r.Frequency,
                        RecurringTransactionId = r.Id,
                        Origin = TransactionOrigin.Manual,
                        ExternalId = externalId,
                        Notes = r.Notes,
                        ExcludeFromReports = r.Type is TransactionType.Transfer or TransactionType.CreditCardPayment
                    });
                    generated++;
                }

                occurrence = NextDate(occurrence, r.Frequency);
            }

            r.NextOccurrence = occurrence;
            r.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (generated > 0 || recurrences.Count > 0)
            await _db.SaveChangesAsync(ct);
        return generated;
    }

    /// <summary>Próxima data a partir de uma data e frequência.</summary>
    public static DateOnly NextDate(DateOnly from, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => from.AddDays(1),
        RecurrenceFrequency.Weekly => from.AddDays(7),
        RecurrenceFrequency.Biweekly => from.AddDays(14),
        RecurrenceFrequency.Monthly => from.AddMonths(1),
        RecurrenceFrequency.Bimonthly => from.AddMonths(2),
        RecurrenceFrequency.Quarterly => from.AddMonths(3),
        RecurrenceFrequency.Semiannual => from.AddMonths(6),
        RecurrenceFrequency.Yearly => from.AddYears(1),
        _ => from.AddMonths(1)
    };

    private static void Validate(SaveRecurringTransactionRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Description))
            throw new AppValidationException(nameof(r.Description), "A descrição é obrigatória.");
        if (r.Amount <= 0)
            throw new AppValidationException(nameof(r.Amount), "O valor deve ser maior que zero.");
        if (r.Frequency == RecurrenceFrequency.None)
            throw new AppValidationException(nameof(r.Frequency), "Escolha uma frequência de recorrência.");
        if (r.EndDate is DateOnly end && end < r.StartDate)
            throw new AppValidationException(nameof(r.EndDate), "A data final deve ser posterior à inicial.");
    }

    private async Task<RecurringTransactionDto> SingleAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.RecurringTransactions.FirstAsync(r => r.Id == id, ct);
        var categories = await _db.Categories.Where(c => c.UserId == UserId).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return Map(entity, categories);
    }

    private static RecurringTransactionDto Map(RecurringTransaction r, IReadOnlyDictionary<Guid, string> categories) => new()
    {
        Id = r.Id,
        Description = r.Description,
        Amount = r.Amount,
        Type = r.Type,
        TypeLabel = r.Type.GetDescription(),
        Frequency = r.Frequency,
        FrequencyLabel = r.Frequency.GetDescription(),
        CategoryId = r.CategoryId,
        CategoryName = r.CategoryId is Guid cid && categories.TryGetValue(cid, out var name) ? name : null,
        AccountId = r.AccountId,
        CreditCardId = r.CreditCardId,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        NextOccurrence = r.NextOccurrence,
        IsActive = r.IsActive,
        Notes = r.Notes
    };
}
