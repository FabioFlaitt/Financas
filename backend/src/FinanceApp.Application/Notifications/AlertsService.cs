using System.Globalization;
using FinanceApp.Application.Budgets;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Notifications;

public record AlertDto
{
    public NotificationType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    /// <summary>"critical", "warning" ou "info".</summary>
    public string Severity { get; init; } = "info";
    public DateOnly? Date { get; init; }
}

public interface IAlertsService
{
    Task<IReadOnlyList<AlertDto>> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Calcula os alertas acionáveis do momento a partir dos dados do usuário (orçamentos no limite,
/// faturas a vencer/vencidas e contas a pagar próximas), respeitando as preferências de notificação.
/// Não envia push nem persiste nada — é uma leitura usada pelo app para montar o painel de avisos.
/// </summary>
public class AlertsService : IAlertsService
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private const int UpcomingDays = 7;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IBudgetService _budgets;

    public AlertsService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IBudgetService budgets)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _budgets = budgets;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<AlertDto>> GetAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var horizon = today.AddDays(UpcomingDays);

        var enabled = (await _db.NotificationPreferences
            .Where(p => p.UserId == UserId && p.Enabled)
            .Select(p => p.Type)
            .ToListAsync(ct)).ToHashSet();

        var alerts = new List<AlertDto>();

        // --- Orçamentos no limite ---
        if (enabled.Contains(NotificationType.BudgetNearLimit) || enabled.Contains(NotificationType.ExpenseOverLimit))
        {
            var overview = await _budgets.GetOverviewAsync(today.Year, today.Month, ct);
            foreach (var b in overview.Items)
            {
                if (b.IsOverLimit && enabled.Contains(NotificationType.ExpenseOverLimit))
                    alerts.Add(new AlertDto
                    {
                        Type = NotificationType.ExpenseOverLimit,
                        TypeLabel = NotificationType.ExpenseOverLimit.GetDescription(),
                        Title = $"Orçamento estourado: {b.CategoryName}",
                        Detail = $"Gasto de {Money(b.SpentAmount)} de {Money(b.LimitAmount)} ({b.UsedPercent:0}%).",
                        Severity = "critical"
                    });
                else if (b.IsAlert && enabled.Contains(NotificationType.BudgetNearLimit))
                    alerts.Add(new AlertDto
                    {
                        Type = NotificationType.BudgetNearLimit,
                        TypeLabel = NotificationType.BudgetNearLimit.GetDescription(),
                        Title = $"Orçamento perto do limite: {b.CategoryName}",
                        Detail = $"Já usou {b.UsedPercent:0}% de {Money(b.LimitAmount)}.",
                        Severity = "warning"
                    });
            }
        }

        // --- Faturas de cartão a vencer/vencidas ---
        if (enabled.Contains(NotificationType.InvoiceDue))
        {
            var invoices = await _db.CreditCardInvoices
                .Where(i => i.UserId == UserId && i.Status != InvoiceStatus.Paid && i.TotalAmount > i.PaidAmount)
                .Where(i => i.DueDate <= horizon)
                .Include(i => i.CreditCard)
                .ToListAsync(ct);

            foreach (var i in invoices)
            {
                var outstanding = i.TotalAmount - i.PaidAmount;
                var overdue = i.DueDate < today;
                alerts.Add(new AlertDto
                {
                    Type = NotificationType.InvoiceDue,
                    TypeLabel = NotificationType.InvoiceDue.GetDescription(),
                    Title = overdue
                        ? $"Fatura vencida: {i.CreditCard?.Name}"
                        : $"Fatura a vencer: {i.CreditCard?.Name}",
                    Detail = $"{Money(outstanding)} em aberto, vence em {i.DueDate.ToString("dd/MM", PtBr)}.",
                    Severity = overdue ? "critical" : "warning",
                    Date = i.DueDate
                });
            }
        }

        // --- Contas a pagar próximas (lançamentos previstos/pendentes) ---
        if (enabled.Contains(NotificationType.BillDue))
        {
            var pending = await _db.Transactions
                .Where(t => t.UserId == UserId &&
                            t.Type == TransactionType.Expense &&
                            (t.Status == TransactionStatus.Forecast || t.Status == TransactionStatus.Pending))
                .Select(t => new { t.Description, t.Amount, t.DueDate, t.TransactionDate })
                .ToListAsync(ct);

            foreach (var t in pending)
            {
                var due = t.DueDate ?? t.TransactionDate;
                if (due > horizon) continue;
                var overdue = due < today;
                alerts.Add(new AlertDto
                {
                    Type = NotificationType.BillDue,
                    TypeLabel = NotificationType.BillDue.GetDescription(),
                    Title = overdue ? $"Conta vencida: {t.Description}" : $"Conta a vencer: {t.Description}",
                    Detail = $"{Money(t.Amount)}, vence em {due.ToString("dd/MM", PtBr)}.",
                    Severity = overdue ? "critical" : "warning",
                    Date = due
                });
            }
        }

        return alerts
            .OrderBy(a => SeverityRank(a.Severity))
            .ThenBy(a => a.Date ?? DateOnly.MaxValue)
            .ToList();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2
    };

    private static string Money(decimal value) => value.ToString("C2", PtBr);
}
