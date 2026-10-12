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
