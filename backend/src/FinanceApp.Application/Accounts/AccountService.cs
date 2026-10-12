using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Accounts;

public record AccountDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public AccountType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public decimal InitialBalance { get; init; }
    public decimal CurrentBalance { get; init; }
    public string ColorHex { get; init; } = "#1565C0";
    public string Icon { get; init; } = "bank";
    public bool IncludeInTotalBalance { get; init; }
    public bool IsArchived { get; init; }
}

public record SaveAccountRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public AccountType Type { get; init; } = AccountType.Checking;
    public decimal InitialBalance { get; init; }
    public string ColorHex { get; init; } = "#1565C0";
    public string Icon { get; init; } = "bank";
    public bool IncludeInTotalBalance { get; init; } = true;
}

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken ct = default);
    Task<AccountDto> CreateAsync(SaveAccountRequest request, CancellationToken ct = default);
    Task<AccountDto> UpdateAsync(Guid id, SaveAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class AccountService : IAccountService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AccountService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken ct = default)
    {
        var accounts = await _db.Accounts.Where(a => a.UserId == UserId).ToListAsync(ct);
        var ids = accounts.Select(a => a.Id).ToHashSet();

        // Movimentos confirmados que afetam o caixa, para derivar o saldo atual de cada conta.
        var movements = await _db.Transactions
            .Where(t => t.UserId == UserId && t.Status == TransactionStatus.Confirmed)
            .Where(t => t.CreditCardId == null) // compras no cartão não afetam o caixa
            .Select(t => new { t.Type, t.Amount, t.AccountId, t.DestinationAccountId })
            .ToListAsync(ct);

        var deltas = accounts.ToDictionary(a => a.Id, a => 0m);
        foreach (var m in movements)
        {
            switch (m.Type)
            {
                case TransactionType.Income or TransactionType.Refund:
                    if (m.AccountId is Guid a1 && deltas.ContainsKey(a1)) deltas[a1] += m.Amount;
                    break;
                case TransactionType.Expense or TransactionType.CreditCardPayment:
                    if (m.AccountId is Guid a2 && deltas.ContainsKey(a2)) deltas[a2] -= m.Amount;
                    break;
                case TransactionType.Transfer:
                    if (m.AccountId is Guid a3 && deltas.ContainsKey(a3)) deltas[a3] -= m.Amount;
                    if (m.DestinationAccountId is Guid a4 && deltas.ContainsKey(a4)) deltas[a4] += m.Amount;
                    break;
            }
        }

        return accounts
            .OrderBy(a => a.IsArchived).ThenBy(a => a.Name)
            .Select(a => Map(a, a.InitialBalance + deltas[a.Id]))
            .ToList();
    }

    public async Task<AccountDto> CreateAsync(SaveAccountRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var account = new Account
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            Institution = request.Institution?.Trim(),
            Type = request.Type,
            InitialBalance = request.InitialBalance,
            ColorHex = request.ColorHex,
            Icon = request.Icon,
            IncludeInTotalBalance = request.IncludeInTotalBalance
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return Map(account, account.InitialBalance);
    }

    public async Task<AccountDto> UpdateAsync(Guid id, SaveAccountRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId, ct)
            ?? throw NotFoundException.For("Conta", id);
        account.Name = request.Name.Trim();
        account.Institution = request.Institution?.Trim();
        account.Type = request.Type;
        account.InitialBalance = request.InitialBalance;
        account.ColorHex = request.ColorHex;
        account.Icon = request.Icon;
        account.IncludeInTotalBalance = request.IncludeInTotalBalance;
        account.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var list = await ListAsync(ct);
        return list.First(a => a.Id == id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId, ct)
            ?? throw NotFoundException.For("Conta", id);

        var hasTx = await _db.Transactions.AnyAsync(
            t => t.UserId == UserId && (t.AccountId == id || t.DestinationAccountId == id), ct);
        if (hasTx)
        {
            // Preserva o histórico: arquiva em vez de excluir contas com lançamentos.
            account.IsArchived = true;
            account.IncludeInTotalBalance = false;
            account.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _db.Accounts.Remove(account);
        }
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveAccountRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new AppValidationException(nameof(r.Name), "O nome da conta é obrigatório.");
    }

    private static AccountDto Map(Account a, decimal currentBalance) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Institution = a.Institution,
        Type = a.Type,
        TypeLabel = a.Type.GetDescription(),
        InitialBalance = a.InitialBalance,
        CurrentBalance = Math.Round(currentBalance, 2),
        ColorHex = a.ColorHex,
        Icon = a.Icon,
        IncludeInTotalBalance = a.IncludeInTotalBalance,
        IsArchived = a.IsArchived
    };
}
