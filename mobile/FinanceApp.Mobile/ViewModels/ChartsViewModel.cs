using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Drawables;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class ChartsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public ChartsViewModel(FinanceApiClient api)
    {
        _api = api;
        var now = DateTime.Today;
        _year = now.Year;
        _month = now.Month;
        UpdateMonthLabel();
    }

    private int _year;
    private int _month;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _monthLabel = string.Empty;

    [ObservableProperty] private string _incomeLabel = "R$ 0,00";
    [ObservableProperty] private string _expenseLabel = "R$ 0,00";
    [ObservableProperty] private string _resultLabel = "R$ 0,00";
    [ObservableProperty] private decimal _result;
    [ObservableProperty] private string _fixedLabel = "R$ 0,00";
    [ObservableProperty] private string _variableLabel = "R$ 0,00";

    [ObservableProperty] private IDrawable? _monthlySeriesChart;
    [ObservableProperty] private IDrawable? _categoryChart;
    [ObservableProperty] private IDrawable? _fixedVariableChart;

    public IReadOnlyList<CategorySlice> Categories { get; private set; } = new List<CategorySlice>();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var report = await _api.GetReportSummaryAsync(_year, _month, months: 6);
            Apply(report);
            HasData = true;
        }
        catch
        {
            HasData = false;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        var d = new DateTime(_year, _month, 1).AddMonths(-1);
        _year = d.Year; _month = d.Month;
        UpdateMonthLabel();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        var d = new DateTime(_year, _month, 1).AddMonths(1);
        _year = d.Year; _month = d.Month;
        UpdateMonthLabel();
        await LoadAsync();
    }

    private void Apply(ReportSummary report)
    {
        IncomeLabel = report.MonthIncome.ToString("C2", BrazilCulture.PtBr);
        ExpenseLabel = report.MonthExpense.ToString("C2", BrazilCulture.PtBr);
        Result = report.MonthResult;
        ResultLabel = report.MonthResult.ToString("C2", BrazilCulture.PtBr);
        FixedLabel = report.FixedVsVariable.Fixed.ToString("C2", BrazilCulture.PtBr);
        VariableLabel = report.FixedVsVariable.Variable.ToString("C2", BrazilCulture.PtBr);

        var hole = Color.FromArgb("#FFFFFF");
        MonthlySeriesChart = new BarChartDrawable(report.MonthlySeries);
        CategoryChart = new DonutChartDrawable(report.ExpensesByCategory, hole);

        var fixedVariableSlices = new List<CategorySlice>
        {
            new() { CategoryName = "Fixas", ColorHex = "#1565C0", Amount = report.FixedVsVariable.Fixed, Percent = report.FixedVsVariable.FixedPercent },
            new() { CategoryName = "Variáveis", ColorHex = "#EF6C00", Amount = report.FixedVsVariable.Variable, Percent = report.FixedVsVariable.VariablePercent }
        };
        FixedVariableChart = new DonutChartDrawable(fixedVariableSlices, hole);

        Categories = report.ExpensesByCategory;
        OnPropertyChanged(nameof(Categories));
    }

    private void UpdateMonthLabel()
    {
        var label = new DateTime(_year, _month, 1).ToString("MMMM 'de' yyyy", BrazilCulture.PtBr);
        MonthLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
    }
}
