namespace FinanceApp.Mobile.Models;

// Espelham os DTOs da API (JSON camelCase). Mantidos simples propositalmente.

public class AuthResult
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class DashboardResult
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal MonthIncome { get; set; }
    public decimal MonthExpense { get; set; }
    public decimal MonthResult { get; set; }
    public decimal OpenInvoicesTotal { get; set; }
    public decimal RealizedExpenses { get; set; }
    public decimal ForecastExpenses { get; set; }
    public decimal CommittedIncomePercent { get; set; }
    public decimal PreviousMonthExpense { get; set; }
    public decimal? ExpenseVsPreviousMonthPercent { get; set; }
    public decimal ProjectedEndOfMonthBalance { get; set; }
    public List<DailyBalancePoint> DailyBalanceEvolution { get; set; } = new();
    public List<CategorySlice> ExpensesByCategory { get; set; } = new();
    public List<TopExpenseItem> TopExpenses { get; set; } = new();
}

public class DailyBalancePoint
{
    public DateOnly Date { get; set; }
    public decimal Balance { get; set; }
}

public class CategorySlice
{
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#9E9E9E";
    public decimal Amount { get; set; }
    public decimal Percent { get; set; }
}

public class TopExpenseItem
{
    public Guid TransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
}

// ---- Relatórios / análises (Fase 3) ----

public class ReportSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal MonthIncome { get; set; }
    public decimal MonthExpense { get; set; }
    public decimal MonthResult { get; set; }
    public List<MonthlyPoint> MonthlySeries { get; set; } = new();
    public List<CategorySlice> ExpensesByCategory { get; set; } = new();
    public FixedVariable FixedVsVariable { get; set; } = new();
}

public class MonthlyPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Result { get; set; }
}

public class FixedVariable
{
    public decimal Fixed { get; set; }
    public decimal Variable { get; set; }
    public decimal FixedPercent { get; set; }
    public decimal VariablePercent { get; set; }
}

// ---- Orçamento mensal (Fase 3) ----

public class BudgetOverview
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalLimit { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalRemaining { get; set; }
    public decimal UsedPercent { get; set; }
    public bool HasTotalBudget { get; set; }
    public List<BudgetItem> Items { get; set; } = new();
}

public class BudgetItem
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = "Orçamento total";
    public string CategoryColorHex { get; set; } = "#0D47A1";
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal LimitAmount { get; set; }
    public int AlertThresholdPercent { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsedPercent { get; set; }
    public bool IsOverLimit { get; set; }
    public bool IsAlert { get; set; }

    /// <summary>Fração 0–1 para a barra de progresso (limitada a 100%).</summary>
    public double ProgressFraction => LimitAmount <= 0 ? 0 : Math.Min(1.0, (double)(SpentAmount / LimitAmount));

    /// <summary>Cor da barra conforme o consumo: vermelho (estourou), âmbar (alerta) ou verde.</summary>
    public string BarColorHex => IsOverLimit ? "#C62828" : IsAlert ? "#F9A825" : "#2E7D32";
}

// ---- Cartões e faturas (Fase 4) ----

public class CardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Brand { get; set; }
    public decimal? CreditLimit { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public string ColorHex { get; set; } = "#6A1B9A";
    public string Icon { get; set; } = "credit-card";
    public Guid? DefaultPaymentAccountId { get; set; }
    public decimal CurrentInvoiceTotal { get; set; }
    public decimal OpenTotal { get; set; }
    public decimal? AvailableLimit { get; set; }
    public decimal? UsedLimitPercent { get; set; }

    public double UsedFraction => CreditLimit is > 0 ? Math.Min(1.0, (double)(OpenTotal / CreditLimit.Value)) : 0;
    public bool HasLimit => CreditLimit is > 0;
}

public class InvoiceListItem
{
    public Guid Id { get; set; }
    public Guid CreditCardId { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    public DateOnly ClosingDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }

    public string StatusColorHex => IsOverdue ? "#C62828"
        : StatusLabel == "Paga" ? "#2E7D32"
        : StatusLabel == "Fechada" ? "#F9A825"
        : "#1565C0";
}

public class InvoiceDetail : InvoiceListItem
{
    public string CardName { get; set; } = string.Empty;
    public List<InvoiceTransaction> Transactions { get; set; } = new();
}

public class InvoiceTransaction
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColorHex { get; set; }
    public bool IsInstallment { get; set; }
}

// ---- Importação de extratos (Fase 5) ----

public class ImportItem
{
    public string ExternalId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
}

public class ImportPreview
{
    public int TotalParsed { get; set; }
    public int Duplicates { get; set; }
    public List<ImportItem> Items { get; set; } = new();
}

public class ImportResult
{
    public int Imported { get; set; }
    public int SkippedDuplicates { get; set; }
}

// ---- Notificações e alertas (Fase 7) ----

public class AlertItem
{
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public DateOnly? Date { get; set; }

    public string SeverityColorHex => Severity switch
    {
        "critical" => "#C62828",
        "warning" => "#F9A825",
        _ => "#1565C0"
    };
}

public class NotificationPreferenceItem
{
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool HideSensitiveOnLockScreen { get; set; }
    public string? PreferredTime { get; set; }
}
