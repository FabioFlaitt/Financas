using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Drawables;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public DashboardViewModel(FinanceApiClient api)
    {
        _api = api;
        var now = DateTime.Today;
        _year = now.Year;
        _month = now.Month;
    }

    private int _year;
    private int _month;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private string _greeting = "Olá!";

    [ObservableProperty] private DashboardResult? _dashboard;
    [ObservableProperty] private IDrawable? _categoryChart;
    [ObservableProperty] private IDrawable? _balanceChart;

    public IReadOnlyList<CategorySlice> Categories => Dashboard?.ExpensesByCategory ?? new List<CategorySlice>();
    public IReadOnlyList<PaymentMethodSlice> PaymentMethods => Dashboard?.ExpensesByPaymentMethod ?? new List<PaymentMethodSlice>();
    public IReadOnlyList<TopExpenseItem> TopExpenses => Dashboard?.TopExpenses ?? new List<TopExpenseItem>();
    public bool HasPaymentMethods => PaymentMethods.Count > 0;
    public string ExpenseCountLabel => (Dashboard?.ExpenseCount ?? 0).ToString();
    public string AverageExpenseLabel => (Dashboard?.AverageExpense ?? 0m).ToString("C2", BrazilCulture.PtBr);

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            Apply(await _api.GetDashboardAsync(_year, _month));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Não foi possível carregar o painel: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(DashboardResult data)
    {
        Dashboard = data;

        var date = new DateTime(data.Year, data.Month, 1);
        var label = date.ToString("MMMM 'de' yyyy", BrazilCulture.PtBr);
        MonthLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);

        var hole = Color.FromArgb("#FFFFFF");
        CategoryChart = new DonutChartDrawable(data.ExpensesByCategory, hole);
        BalanceChart = new LineChartDrawable(data.DailyBalanceEvolution, Color.FromArgb("#0D47A1"));

        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(PaymentMethods));
        OnPropertyChanged(nameof(HasPaymentMethods));
        OnPropertyChanged(nameof(ExpenseCountLabel));
        OnPropertyChanged(nameof(AverageExpenseLabel));
        OnPropertyChanged(nameof(TopExpenses));
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        var d = new DateTime(_year, _month, 1).AddMonths(-1);
        _year = d.Year; _month = d.Month;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        var d = new DateTime(_year, _month, 1).AddMonths(1);
        _year = d.Year; _month = d.Month;
        await LoadAsync();
    }
}
