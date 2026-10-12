using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public TransactionsViewModel(FinanceApiClient api)
    {
        _api = api;
        var now = DateTime.Today;
        _year = now.Year;
        _month = now.Month;
        UpdateMonthLabel();
    }

    public ObservableCollection<TransactionDto> Items { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _incomeLabel = "R$ 0,00";
    [ObservableProperty] private string _expenseLabel = "R$ 0,00";

    private int _year;
    private int _month;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _api.ListTransactionsAsync(_year, _month, SearchText);
            Items.Clear();
            foreach (var t in result.Items) Items.Add(t);
            HasItems = Items.Count > 0;

            var income = Items.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expense = Items.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            IncomeLabel = income.ToString("C2", BrazilCulture.PtBr);
            ExpenseLabel = expense.ToString("C2", BrazilCulture.PtBr);
        }
        catch
        {
            Items.Clear();
            HasItems = false;
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

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    private void UpdateMonthLabel()
    {
        var label = new DateTime(_year, _month, 1).ToString("MMMM 'de' yyyy", BrazilCulture.PtBr);
        MonthLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
    }
}
