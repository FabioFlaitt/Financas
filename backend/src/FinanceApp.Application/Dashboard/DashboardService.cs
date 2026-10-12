using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardResult> GetAsync(DashboardQuery query, CancellationToken ct = default);
}

/// <summary>
/// Calcula os indicadores da tela inicial a partir dos lançamentos reais do usuário.
/// Regras financeiras aplicadas:
/// - compras no cartão de crédito NÃO reduzem o saldo de caixa até o pagamento da fatura;
/// - transferências entre contas próprias não contam como despesa/receita nos relatórios;
/// - pagamentos de fatura reduzem o caixa mas não recontabilizam as compras.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public DashboardService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DashboardResult> GetAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

        var monthStart = new DateOnly(query.Year, query.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var prevStart = monthStart.AddMonths(-1);
        var prevEnd = monthStart.AddDays(-1);
        var today = _clock.Today;

        // Contas que compõem o saldo (respeitando o filtro de conta).
        var accounts = await _db.Accounts
            .Where(a => a.UserId == userId && !a.IsArchived && a.IncludeInTotalBalance)
            .Where(a => query.AccountId == null || a.Id == query.AccountId)
            .ToListAsync(ct);
        var includedAccountIds = accounts.Select(a => a.Id).ToHashSet();
        var baseBalance = accounts.Sum(a => a.InitialBalance);

        // Carrega os lançamentos não cancelados do usuário (volume pessoal; agregação em memória).
        var txQuery = _db.Transactions.Where(t => t.UserId == userId && t.Status != TransactionStatus.Canceled);
        if (query.AccountId != null)
            txQuery = txQuery.Where(t => t.AccountId == query.AccountId || t.DestinationAccountId == query.AccountId);
        if (query.CreditCardId != null)
            txQuery = txQuery.Where(t => t.CreditCardId == query.CreditCardId);
        if (query.CategoryId != null)
            txQuery = txQuery.Where(t => t.CategoryId == query.CategoryId);

        var transactions = await txQuery.ToListAsync(ct);

        var categories = await _db.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
        var categoryById = categories.ToDictionary(c => c.Id);

        // --- Saldo total atual (caixa, confirmados até hoje) ---
        var totalBalance = baseBalance + transactions
            .Where(t => t.Status == TransactionStatus.Confirmed && t.TransactionDate <= today)
            .Sum(t => CashEffect(t, includedAccountIds));

        // --- Receitas e despesas do mês (relatório; inclui compras no cartão) ---
        bool IsReportExpense(Transaction t) =>
            t.Type == TransactionType.Expense && !t.ExcludeFromReports &&
            t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd;

        bool IsReportIncome(Transaction t) =>
            t.Type == TransactionType.Income && !t.ExcludeFromReports &&
            t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd;

        var monthExpenses = transactions.Where(IsReportExpense).ToList();
        var monthIncome = transactions.Where(IsReportIncome).Sum(t => t.Amount);
        var monthExpense = monthExpenses.Sum(t => t.Amount);

        var realizedExpenses = monthExpenses
            .Where(t => t.Status == TransactionStatus.Confirmed)
            .Sum(t => t.Amount);
        var forecastExpenses = monthExpenses
            .Where(t => t.Status is TransactionStatus.Forecast or TransactionStatus.Pending)
            .Sum(t => t.Amount);

        var previousMonthExpense = transactions
            .Where(t => t.Type == TransactionType.Expense && !t.ExcludeFromReports &&
                        t.TransactionDate >= prevStart && t.TransactionDate <= prevEnd)
            .Sum(t => t.Amount);

        decimal? expenseVsPrev = previousMonthExpense > 0
            ? Math.Round((monthExpense - previousMonthExpense) / previousMonthExpense * 100m, 1)
            : null;

        var committedPercent = monthIncome > 0
            ? Math.Round(monthExpense / monthIncome * 100m, 1)
            : 0m;

        // --- Faturas de cartão em aberto ---
        var openInvoicesTotal = await _db.CreditCardInvoices
            .Where(i => i.UserId == userId && i.Status != InvoiceStatus.Paid)
            .Where(i => query.CreditCardId == null || i.CreditCardId == query.CreditCardId)
            .SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), ct) ?? 0m;

        // --- Saldo projetado até o fim do mês (soma fluxos previstos/pendentes após hoje) ---
        var projectedFlow = transactions
            .Where(t => t.Status is TransactionStatus.Forecast or TransactionStatus.Pending)
            .Where(t => t.TransactionDate > today && t.TransactionDate <= monthEnd)
            .Sum(t => CashEffect(t, includedAccountIds));
        var projectedEndOfMonth = totalBalance + projectedFlow;

        // --- Evolução diária do saldo no mês ---
        var startingBalance = baseBalance + transactions
            .Where(t => t.TransactionDate < monthStart &&
                        (t.Status == TransactionStatus.Confirmed ||
                         t.Status is TransactionStatus.Forecast or TransactionStatus.Pending))
            .Sum(t => CashEffect(t, includedAccountIds));

        var dailyByDate = transactions
            .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
            .GroupBy(t => t.TransactionDate)
            .ToDictionary(g => g.Key, g => g.Sum(t => CashEffect(t, includedAccountIds)));

        var evolution = new List<DailyBalancePoint>();
        var running = startingBalance;
        for (var d = monthStart; d <= monthEnd; d = d.AddDays(1))
        {
            if (dailyByDate.TryGetValue(d, out var delta)) running += delta;
            evolution.Add(new DailyBalancePoint(d, Math.Round(running, 2)));
        }

        // --- Despesas por categoria (gráfico de setores) ---
        var byCategory = monthExpenses
            .GroupBy(t => t.CategoryId)
            .Select(g =>
            {
                var amount = g.Sum(t => t.Amount);
                categoryById.TryGetValue(g.Key ?? Guid.Empty, out var cat);
                return new CategorySlice(
                    g.Key,
                    cat?.Name ?? "Sem categoria",
                    cat?.ColorHex ?? "#9E9E9E",
                    Math.Round(amount, 2),
                    monthExpense > 0 ? Math.Round(amount / monthExpense * 100m, 1) : 0m);
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        // --- Cinco maiores despesas ---
        var topExpenses = monthExpenses
            .OrderByDescending(t => t.Amount)
            .Take(5)
            .Select(t =>
            {
                categoryById.TryGetValue(t.CategoryId ?? Guid.Empty, out var cat);
                return new TopExpenseItem(t.Id, t.Description, cat?.Name, Math.Round(t.Amount, 2), t.TransactionDate);
            })
            .ToList();

        return new DashboardResult
        {
            Year = query.Year,
            Month = query.Month,
            TotalBalance = Math.Round(totalBalance, 2),
            MonthIncome = Math.Round(monthIncome, 2),
            MonthExpense = Math.Round(monthExpense, 2),
            OpenInvoicesTotal = Math.Round(openInvoicesTotal, 2),
            RealizedExpenses = Math.Round(realizedExpenses, 2),
            ForecastExpenses = Math.Round(forecastExpenses, 2),
            CommittedIncomePercent = committedPercent,
            PreviousMonthExpense = Math.Round(previousMonthExpense, 2),
            ExpenseVsPreviousMonthPercent = expenseVsPrev,
            ProjectedEndOfMonthBalance = Math.Round(projectedEndOfMonth, 2),
            DailyBalanceEvolution = evolution,
            ExpensesByCategory = byCategory,
            TopExpenses = topExpenses
        };
    }

    /// <summary>
    /// Efeito de um lançamento sobre o saldo de CAIXA (não de relatório).
    /// Compras no cartão não afetam o caixa; pagamentos de fatura e débitos diretos, sim.
    /// </summary>
    private static decimal CashEffect(Transaction t, HashSet<Guid> includedAccounts)
    {
        // Compra no cartão de crédito: entra na fatura, não no caixa.
        bool isCardPurchase = t.CreditCardId.HasValue && t.Type == TransactionType.Expense;
        if (isCardPurchase) return 0m;

        decimal effect = 0m;
        switch (t.Type)
        {
            case TransactionType.Income:
            case TransactionType.Refund:
                if (t.AccountId.HasValue && includedAccounts.Contains(t.AccountId.Value))
                    effect += t.Amount;
                break;

            case TransactionType.Expense:
            case TransactionType.CreditCardPayment:
                if (t.AccountId.HasValue && includedAccounts.Contains(t.AccountId.Value))
                    effect -= t.Amount;
                break;

            case TransactionType.Transfer:
                if (t.AccountId.HasValue && includedAccounts.Contains(t.AccountId.Value))
                    effect -= t.Amount;
                if (t.DestinationAccountId.HasValue && includedAccounts.Contains(t.DestinationAccountId.Value))
                    effect += t.Amount;
                break;
        }
        return effect;
    }
}
