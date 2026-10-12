using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Budgets;

public record BudgetDto
{
    public Guid Id { get; init; }
    public Guid? CategoryId { get; init; }
    public string CategoryName { get; init; } = "Orçamento total";
    public string CategoryColorHex { get; init; } = "#0D47A1";
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal LimitAmount { get; init; }
    public int AlertThresholdPercent { get; init; }

    /// <summary>Despesas realizadas (confirmadas) no período para o escopo do orçamento.</summary>
    public decimal SpentAmount { get; init; }
    /// <summary>Limite menos o realizado (pode ser negativo quando estoura).</summary>
    public decimal RemainingAmount { get; init; }
    /// <summary>Percentual do limite já consumido (0-∞).</summary>
    public decimal UsedPercent { get; init; }
    /// <summary>Verdadeiro quando o realizado ultrapassa o limite.</summary>
    public bool IsOverLimit { get; init; }
    /// <summary>Verdadeiro quando o consumo atingiu o percentual de alerta configurado.</summary>
    public bool IsAlert { get; init; }
}

public record SaveBudgetRequest
{
    public Guid? CategoryId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal LimitAmount { get; init; }
    public int AlertThresholdPercent { get; init; } = 80;
}

/// <summary>Resumo do mês: limites x realizado, com os orçamentos detalhados por categoria.</summary>
public record BudgetOverviewDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TotalLimit { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal TotalRemaining { get; init; }
    public decimal UsedPercent { get; init; }
    public bool HasTotalBudget { get; init; }
    public IReadOnlyList<BudgetDto> Items { get; init; } = new List<BudgetDto>();
}

public interface IBudgetService
{
    Task<BudgetOverviewDto> GetOverviewAsync(int year, int month, CancellationToken ct = default);
    Task<BudgetDto> CreateAsync(SaveBudgetRequest request, CancellationToken ct = default);
    Task<BudgetDto> UpdateAsync(Guid id, SaveBudgetRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Gerencia os orçamentos mensais (total do mês e por categoria) e calcula o consumo com base
/// nas despesas de relatório já realizadas (confirmadas) — a mesma regra usada no painel:
/// <c>Type == Expense</c>, não excluída de relatórios e com status <c>Confirmed</c>.
/// Compras no cartão contam para o orçamento; transferências e pagamentos de fatura não.
/// </summary>
public class BudgetService : IBudgetService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public BudgetService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<BudgetOverviewDto> GetOverviewAsync(int year, int month, CancellationToken ct = default)
    {
        ValidatePeriod(year, month);

        var budgets = await _db.Budgets
            .Where(b => b.UserId == UserId && b.Year == year && b.Month == month)
            .ToListAsync(ct);

        var categories = await _db.Categories
            .Where(c => c.UserId == UserId)
            .ToListAsync(ct);
        var categoryById = categories.ToDictionary(c => c.Id);

        var (spentByCategory, totalSpent) = await ComputeSpendingAsync(year, month, ct);

        var items = budgets
            .Select(b => Map(b, SpentFor(b, spentByCategory, totalSpent), categoryById))
            .OrderBy(i => i.CategoryId == null ? 0 : 1)
            .ThenByDescending(i => i.LimitAmount)
            .ToList();

        var totalBudget = budgets.FirstOrDefault(b => b.CategoryId == null);
        var totalLimit = totalBudget?.LimitAmount ?? budgets.Where(b => b.CategoryId != null).Sum(b => b.LimitAmount);
        var remaining = totalLimit - totalSpent;

        return new BudgetOverviewDto
        {
            Year = year,
            Month = month,
            TotalLimit = Math.Round(totalLimit, 2),
            TotalSpent = Math.Round(totalSpent, 2),
            TotalRemaining = Math.Round(remaining, 2),
            UsedPercent = totalLimit > 0 ? Math.Round(totalSpent / totalLimit * 100m, 1) : 0m,
            HasTotalBudget = totalBudget != null,
            Items = items
        };
    }

    public async Task<BudgetDto> CreateAsync(SaveBudgetRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        await ValidateCategoryAsync(request.CategoryId, ct);

        var exists = await _db.Budgets.AnyAsync(
            b => b.UserId == UserId && b.Year == request.Year && b.Month == request.Month &&
                 b.CategoryId == request.CategoryId, ct);
        if (exists)
            throw new AppValidationException(nameof(request.CategoryId),
                "Já existe um orçamento para esse período e essa categoria.");

        var budget = new Budget
        {
            UserId = UserId,
            CategoryId = request.CategoryId,
            Year = request.Year,
            Month = request.Month,
            LimitAmount = request.LimitAmount,
            AlertThresholdPercent = Math.Clamp(request.AlertThresholdPercent, 1, 100)
        };
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync(ct);

        return await BuildSingleAsync(budget, ct);
    }

    public async Task<BudgetDto> UpdateAsync(Guid id, SaveBudgetRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        await ValidateCategoryAsync(request.CategoryId, ct);

        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == UserId, ct)
            ?? throw NotFoundException.For("Orçamento", id);

        var clash = await _db.Budgets.AnyAsync(
            b => b.UserId == UserId && b.Id != id && b.Year == request.Year && b.Month == request.Month &&
                 b.CategoryId == request.CategoryId, ct);
        if (clash)
            throw new AppValidationException(nameof(request.CategoryId),
                "Já existe um orçamento para esse período e essa categoria.");

        budget.CategoryId = request.CategoryId;
        budget.Year = request.Year;
        budget.Month = request.Month;
        budget.LimitAmount = request.LimitAmount;
        budget.AlertThresholdPercent = Math.Clamp(request.AlertThresholdPercent, 1, 100);
        budget.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await BuildSingleAsync(budget, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == UserId, ct)
            ?? throw NotFoundException.For("Orçamento", id);
        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync(ct);
    }

    // --- cálculo de consumo ---

    private async Task<(Dictionary<Guid, decimal> byCategory, decimal total)> ComputeSpendingAsync(
        int year, int month, CancellationToken ct)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var expenses = await _db.Transactions
            .Where(t => t.UserId == UserId &&
                        t.Type == TransactionType.Expense &&
                        !t.ExcludeFromReports &&
                        t.Status == TransactionStatus.Confirmed &&
                        t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
            .Select(t => new { t.CategoryId, t.Amount })
            .ToListAsync(ct);

        var byCategory = expenses
            .Where(e => e.CategoryId != null)
            .GroupBy(e => e.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var total = expenses.Sum(e => e.Amount);
        return (byCategory, total);
    }

    private static decimal SpentFor(Budget b, Dictionary<Guid, decimal> byCategory, decimal total)
        => b.CategoryId is Guid cat
            ? (byCategory.TryGetValue(cat, out var v) ? v : 0m)
            : total;

    private async Task<BudgetDto> BuildSingleAsync(Budget budget, CancellationToken ct)
    {
        var (byCategory, total) = await ComputeSpendingAsync(budget.Year, budget.Month, ct);
        var categories = await _db.Categories.Where(c => c.UserId == UserId).ToListAsync(ct);
        return Map(budget, SpentFor(budget, byCategory, total), categories.ToDictionary(c => c.Id));
    }

    private static BudgetDto Map(Budget b, decimal spent, IReadOnlyDictionary<Guid, Category> categoryById)
    {
        var used = b.LimitAmount > 0 ? Math.Round(spent / b.LimitAmount * 100m, 1) : 0m;
        Category? category = b.CategoryId is Guid id && categoryById.TryGetValue(id, out var c) ? c : null;
        return new BudgetDto
        {
            Id = b.Id,
            CategoryId = b.CategoryId,
            CategoryName = category?.Name ?? "Orçamento total",
            CategoryColorHex = category?.ColorHex ?? "#0D47A1",
            Year = b.Year,
            Month = b.Month,
            LimitAmount = Math.Round(b.LimitAmount, 2),
            AlertThresholdPercent = b.AlertThresholdPercent,
            SpentAmount = Math.Round(spent, 2),
            RemainingAmount = Math.Round(b.LimitAmount - spent, 2),
            UsedPercent = used,
            IsOverLimit = spent > b.LimitAmount,
            IsAlert = used >= b.AlertThresholdPercent
        };
    }

    // --- validações ---

    private static void ValidateRequest(SaveBudgetRequest r)
    {
        ValidatePeriod(r.Year, r.Month);
        if (r.LimitAmount <= 0)
            throw new AppValidationException(nameof(r.LimitAmount), "O limite deve ser maior que zero.");
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year < 2000 || year > 2100)
            throw new AppValidationException(nameof(year), "Ano inválido.");
        if (month < 1 || month > 12)
            throw new AppValidationException(nameof(month), "Mês inválido.");
    }

    private async Task ValidateCategoryAsync(Guid? categoryId, CancellationToken ct)
    {
        if (categoryId is Guid id &&
            !await _db.Categories.AnyAsync(c => c.Id == id && c.UserId == UserId, ct))
            throw new AppValidationException(nameof(categoryId), "Categoria inválida.");
    }
}
