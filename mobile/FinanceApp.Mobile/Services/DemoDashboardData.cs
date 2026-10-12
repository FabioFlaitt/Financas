using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Dados de DEMONSTRAÇÃO (fictícios) usados quando a API não está acessível, para que a
/// interface tenha conteúdo no primeiro uso. Sempre identificados como demonstração na tela.
/// </summary>
public static class DemoDashboardData
{
    public static DashboardResult Build()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        var evolution = new List<DailyBalancePoint>();
        decimal running = 7150m;
        var rnd = new Random(42);
        for (int d = 1; d <= daysInMonth; d++)
        {
            if (d == 5) running += 7500m;
            if (d == 10) running -= 2200m;
            if (d % 3 == 0) running -= rnd.Next(30, 120);
            evolution.Add(new DailyBalancePoint { Date = new DateOnly(today.Year, today.Month, d), Balance = Math.Round(running, 2) });
        }

        return new DashboardResult
        {
            Year = today.Year,
            Month = today.Month,
            TotalBalance = 11590m,
            MonthIncome = 7500m,
            MonthExpense = 3870m,
            MonthResult = 3630m,
            OpenInvoicesTotal = 510m,
            RealizedExpenses = 3570m,
            ForecastExpenses = 300m,
            CommittedIncomePercent = 51.6m,
            PreviousMonthExpense = 3600m,
            ExpenseVsPreviousMonthPercent = 7.5m,
            ProjectedEndOfMonthBalance = 11290m,
            ExpenseCount = 24,
            AverageExpense = 161.25m,
            ExpensesByPaymentMethod = new List<PaymentMethodSlice>
            {
                new() { Method = "Credit", MethodLabel = "Crédito", ColorHex = "#6A1B9A", Amount = 1800m, Percent = 46.5m },
                new() { Method = "Debit", MethodLabel = "Débito", ColorHex = "#1565C0", Amount = 1100m, Percent = 28.4m },
                new() { Method = "Pix", MethodLabel = "Pix", ColorHex = "#00838F", Amount = 620m, Percent = 16.0m },
                new() { Method = "Cash", MethodLabel = "Dinheiro", ColorHex = "#2E7D32", Amount = 350m, Percent = 9.0m },
            },
            DailyBalanceEvolution = evolution,
            ExpensesByCategory = new List<CategorySlice>
            {
                new() { CategoryName = "Moradia", ColorHex = "#5E35B1", Amount = 2200m, Percent = 56.8m },
                new() { CategoryName = "Alimentação e supermercado", ColorHex = "#2E7D32", Amount = 770m, Percent = 19.9m },
                new() { CategoryName = "Transporte e combustível", ColorHex = "#1565C0", Amount = 250m, Percent = 6.5m },
                new() { CategoryName = "Restaurantes e delivery", ColorHex = "#EF6C00", Amount = 205m, Percent = 5.3m },
                new() { CategoryName = "Contas de consumo", ColorHex = "#00695C", Amount = 180m, Percent = 4.7m },
                new() { CategoryName = "Saúde e farmácia", ColorHex = "#C62828", Amount = 90m, Percent = 2.3m },
                new() { CategoryName = "Assinaturas e serviços digitais", ColorHex = "#00838F", Amount = 175m, Percent = 4.5m },
            },
            TopExpenses = new List<TopExpenseItem>
            {
                new() { Description = "Aluguel", CategoryName = "Moradia", Amount = 2200m, Date = new DateOnly(today.Year, today.Month, Math.Min(10, daysInMonth)) },
                new() { Description = "Supermercado Pão de Açúcar", CategoryName = "Alimentação e supermercado", Amount = 450m, Date = new DateOnly(today.Year, today.Month, Math.Min(3, daysInMonth)) },
                new() { Description = "Supermercado Carrefour", CategoryName = "Alimentação e supermercado", Amount = 320m, Date = new DateOnly(today.Year, today.Month, Math.Min(11, daysInMonth)) },
                new() { Description = "Posto Shell", CategoryName = "Transporte e combustível", Amount = 250m, Date = new DateOnly(today.Year, today.Month, Math.Min(7, daysInMonth)) },
                new() { Description = "Restaurante Outback", CategoryName = "Restaurantes e delivery", Amount = 120m, Date = new DateOnly(today.Year, today.Month, Math.Min(9, daysInMonth)) },
            }
        };
    }
}
