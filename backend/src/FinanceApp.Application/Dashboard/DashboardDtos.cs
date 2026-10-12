namespace FinanceApp.Application.Dashboard;

/// <summary>Filtros aplicáveis ao dashboard e às análises.</summary>
public record DashboardQuery
{
    public int Year { get; init; }
    public int Month { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CreditCardId { get; init; }
    public Guid? CategoryId { get; init; }
}

/// <summary>Conjunto completo de indicadores exibidos na tela inicial.</summary>
public record DashboardResult
{
    public int Year { get; init; }
    public int Month { get; init; }

    /// <summary>Saldo consolidado das contas marcadas para compor o total.</summary>
    public decimal TotalBalance { get; init; }

    public decimal MonthIncome { get; init; }
    public decimal MonthExpense { get; init; }

    /// <summary>Receitas menos despesas no mês.</summary>
    public decimal MonthResult => MonthIncome - MonthExpense;

    /// <summary>Soma das faturas de cartão em aberto (não pagas).</summary>
    public decimal OpenInvoicesTotal { get; init; }

    public decimal RealizedExpenses { get; init; }
    public decimal ForecastExpenses { get; init; }

    /// <summary>Percentual da renda do mês já comprometida com despesas (0-100+).</summary>
    public decimal CommittedIncomePercent { get; init; }

    public decimal PreviousMonthExpense { get; init; }

    /// <summary>Variação percentual das despesas em relação ao mês anterior (positivo = aumento).</summary>
    public decimal? ExpenseVsPreviousMonthPercent { get; init; }

    /// <summary>Saldo projetado até o fim do mês (inclui previstos/pendentes).</summary>
    public decimal ProjectedEndOfMonthBalance { get; init; }

    /// <summary>Quantidade de lançamentos de despesa no mês (para análises e ticket médio).</summary>
    public int ExpenseCount { get; init; }

    /// <summary>Ticket médio das despesas do mês (despesa total ÷ nº de lançamentos).</summary>
    public decimal AverageExpense { get; init; }

    public IReadOnlyList<DailyBalancePoint> DailyBalanceEvolution { get; init; } = Array.Empty<DailyBalancePoint>();
    public IReadOnlyList<CategorySlice> ExpensesByCategory { get; init; } = Array.Empty<CategorySlice>();
    public IReadOnlyList<PaymentMethodSlice> ExpensesByPaymentMethod { get; init; } = Array.Empty<PaymentMethodSlice>();
    public IReadOnlyList<TopExpenseItem> TopExpenses { get; init; } = Array.Empty<TopExpenseItem>();
}

public record DailyBalancePoint(DateOnly Date, decimal Balance);

public record CategorySlice(Guid? CategoryId, string CategoryName, string ColorHex, decimal Amount, decimal Percent);

public record PaymentMethodSlice(string Method, string MethodLabel, string ColorHex, decimal Amount, decimal Percent);

public record TopExpenseItem(Guid TransactionId, string Description, string? CategoryName, decimal Amount, DateOnly Date);
