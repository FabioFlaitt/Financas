using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Reports;

public record MonthlyPoint(int Year, int Month, string Label, decimal Income, decimal Expense, decimal Result);

public record CategorySpend(Guid? CategoryId, string CategoryName, string ColorHex, decimal Amount, decimal Percent);

public record FixedVariableDto(decimal Fixed, decimal Variable, decimal FixedPercent, decimal VariablePercent);

/// <summary>Relatório consolidado para a aba de gráficos/análises.</summary>
public record ReportSummaryDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal MonthIncome { get; init; }
    public decimal MonthExpense { get; init; }
    public decimal MonthResult { get; init; }
    public IReadOnlyList<MonthlyPoint> MonthlySeries { get; init; } = new List<MonthlyPoint>();
    public IReadOnlyList<CategorySpend> ExpensesByCategory { get; init; } = new List<CategorySpend>();
    public FixedVariableDto FixedVsVariable { get; init; } = new(0m, 0m, 0m, 0m);
}

public interface IReportService
{
    Task<ReportSummaryDto> GetSummaryAsync(int year, int month, int months, CancellationToken ct = default);
}

/// <summary>
/// Monta os dados das análises a partir dos lançamentos de relatório do usuário
/// (receitas/despesas não excluídas de relatórios, excluindo lançamentos cancelados),
/// coerente com os totais exibidos no painel.
/// </summary>
public class ReportService : IReportService
{
    private static readonly string[] MonthAbbr =
        { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReportService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<ReportSummaryDto> GetSummaryAsync(int year, int month, int months, CancellationToken ct = default)
    {
        if (year < 2000 || year > 2100) throw new ArgumentOutOfRangeException(nameof(year));
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        months = Math.Clamp(months, 1, 24);

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var seriesStart = monthStart.AddMonths(-(months - 1));

        // Lançamentos de relatório na janela da série (receita/despesa, não excluídos, não cancelados).
        var raw = await _db.Transactions
            .Where(t => t.UserId == UserId &&
                        t.Status != TransactionStatus.Canceled &&
                        !t.ExcludeFromReports &&
                        (t.Type == TransactionType.Income || t.Type == TransactionType.Expense) &&
                        t.TransactionDate >= seriesStart && t.TransactionDate <= monthEnd)
            .Select(t => new { t.Type, t.Nature, t.CategoryId, t.Amount, t.TransactionDate })
            .ToListAsync(ct);
        var tx = raw
            .Select(t => new TxRow(t.Type, t.Nature, t.CategoryId, t.Amount, t.TransactionDate))
            .ToList();

        var categories = await _db.Categories.Where(c => c.UserId == UserId).ToListAsync(ct);
        var categoryById = categories.ToDictionary(c => c.Id);

        // --- Série mensal receitas x despesas ---
        var series = new List<MonthlyPoint>();
        for (var i = 0; i < months; i++)
        {
            var mStart = seriesStart.AddMonths(i);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var inMonth = tx.Where(t => t.Date >= mStart && t.Date <= mEnd).ToList();
            var income = inMonth.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expense = inMonth.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            series.Add(new MonthlyPoint(
                mStart.Year, mStart.Month, $"{MonthAbbr[mStart.Month - 1]}/{mStart.Year % 100:00}",
                Math.Round(income, 2), Math.Round(expense, 2), Math.Round(income - expense, 2)));
        }

        // --- Mês selecionado ---
        var monthTx = tx.Where(t => t.Date >= monthStart && t.Date <= monthEnd).ToList();
        var monthIncome = monthTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var monthExpenses = monthTx.Where(t => t.Type == TransactionType.Expense).ToList();
        var monthExpense = monthExpenses.Sum(t => t.Amount);

        // --- Despesas por categoria ---
        var byCategory = monthExpenses
            .GroupBy(t => t.CategoryId)
            .Select(g =>
            {
                var amount = g.Sum(t => t.Amount);
                Category? cat = g.Key is Guid id && categoryById.TryGetValue(id, out var c) ? c : null;
                return new CategorySpend(
                    g.Key,
                    cat?.Name ?? "Sem categoria",
                    cat?.ColorHex ?? "#9E9E9E",
                    Math.Round(amount, 2),
                    monthExpense > 0 ? Math.Round(amount / monthExpense * 100m, 1) : 0m);
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        // --- Fixas x variáveis (natureza do lançamento; "não se aplica" entra como variável) ---
        var fixedExpense = monthExpenses.Where(t => t.Nature == ExpenseNature.Fixed).Sum(t => t.Amount);
        var variableExpense = monthExpense - fixedExpense;
        var fixedVariable = new FixedVariableDto(
            Math.Round(fixedExpense, 2),
            Math.Round(variableExpense, 2),
            monthExpense > 0 ? Math.Round(fixedExpense / monthExpense * 100m, 1) : 0m,
            monthExpense > 0 ? Math.Round(variableExpense / monthExpense * 100m, 1) : 0m);

        return new ReportSummaryDto
        {
            Year = year,
            Month = month,
            MonthIncome = Math.Round(monthIncome, 2),
            MonthExpense = Math.Round(monthExpense, 2),
            MonthResult = Math.Round(monthIncome - monthExpense, 2),
            MonthlySeries = series,
            ExpensesByCategory = byCategory,
            FixedVsVariable = fixedVariable
        };
    }

    private readonly record struct TxRow(TransactionType Type, ExpenseNature Nature, Guid? CategoryId, decimal Amount, DateOnly Date);
}
