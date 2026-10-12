using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>Resumo dos orçamentos do mês com consumo realizado por categoria.</summary>
public partial class BudgetViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public BudgetViewModel(FinanceApiClient api)
    {
        _api = api;
        var now = DateTime.Today;
        Year = now.Year;
        Month = now.Month;
        UpdateMonthLabel();
    }

    public int Year { get; private set; }
    public int Month { get; private set; }

    public ObservableCollection<BudgetItem> Items { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private bool _hasTotalBudget;
    [ObservableProperty] private string _totalLimitLabel = "R$ 0,00";
    [ObservableProperty] private string _totalSpentLabel = "R$ 0,00";
    [ObservableProperty] private string _totalRemainingLabel = "R$ 0,00";
    [ObservableProperty] private double _totalProgress;
    [ObservableProperty] private string _usedPercentLabel = "0%";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var overview = await _api.GetBudgetsAsync(Year, Month);
            Items.Clear();
            foreach (var i in overview.Items) Items.Add(i);
            HasItems = Items.Count > 0;

            HasTotalBudget = overview.HasTotalBudget;
            TotalLimitLabel = overview.TotalLimit.ToString("C2", BrazilCulture.PtBr);
            TotalSpentLabel = overview.TotalSpent.ToString("C2", BrazilCulture.PtBr);
            TotalRemainingLabel = overview.TotalRemaining.ToString("C2", BrazilCulture.PtBr);
            TotalProgress = overview.TotalLimit <= 0 ? 0 : Math.Min(1.0, (double)(overview.TotalSpent / overview.TotalLimit));
            UsedPercentLabel = overview.UsedPercent.ToString("0.#", BrazilCulture.PtBr) + "%";
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
        var d = new DateTime(Year, Month, 1).AddMonths(-1);
        Year = d.Year; Month = d.Month;
        UpdateMonthLabel();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        var d = new DateTime(Year, Month, 1).AddMonths(1);
        Year = d.Year; Month = d.Month;
        UpdateMonthLabel();
        await LoadAsync();
    }

    public async Task<bool> DeleteAsync(BudgetItem item)
    {
        try { await _api.DeleteBudgetAsync(item.Id); await LoadAsync(); return true; }
        catch { return false; }
    }

    private void UpdateMonthLabel()
    {
        var label = new DateTime(Year, Month, 1).ToString("MMMM 'de' yyyy", BrazilCulture.PtBr);
        MonthLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
    }
}

/// <summary>Criação/edição de um orçamento (total do mês ou por categoria).</summary>
public partial class BudgetEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _id;

    public BudgetEditViewModel(FinanceApiClient api) => _api = api;

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Novo orçamento";
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private string _limitText = string.Empty;
    [ObservableProperty] private double _alertThreshold = 80;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PickerOption<Guid?>> Categories { get; } = new();
    [ObservableProperty] private PickerOption<Guid?>? _selectedCategory;

    public string AlertThresholdLabel => $"Alertar em {AlertThreshold:0}%";
    partial void OnAlertThresholdChanged(double value) => OnPropertyChanged(nameof(AlertThresholdLabel));

    public async Task InitializeAsync(BudgetItem? item, int year, int month)
    {
        Year = item?.Year ?? year;
        Month = item?.Month ?? month;
        PeriodLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            new DateTime(Year, Month, 1).ToString("MMMM 'de' yyyy", BrazilCulture.PtBr));

        Categories.Clear();
        Categories.Add(new PickerOption<Guid?>(null, "Orçamento total do mês"));
        try
        {
            var cats = await _api.ListCategoriesAsync();
            foreach (var c in cats) Categories.Add(new PickerOption<Guid?>(c.Id, c.Name));
        }
        catch { /* mantém ao menos a opção de orçamento total */ }

        if (item is null)
        {
            SelectedCategory = Categories[0];
            return;
        }

        _id = item.Id;
        IsEditing = true;
        Title = "Editar orçamento";
        LimitText = item.LimitAmount.ToString("0.00", BrazilCulture.PtBr);
        AlertThreshold = item.AlertThresholdPercent;
        SelectedCategory = Categories.FirstOrDefault(o => o.Value == item.CategoryId) ?? Categories[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (!MoneyParser.TryParse(LimitText, out var limit) || limit <= 0)
        {
            ErrorMessage = "Informe um limite válido.";
            return;
        }

        var body = new
        {
            categoryId = SelectedCategory?.Value,
            year = Year,
            month = Month,
            limitAmount = limit,
            alertThresholdPercent = (int)Math.Round(AlertThreshold)
        };
        IsBusy = true;
        try
        {
            if (_id is Guid id) await _api.UpdateBudgetAsync(id, body);
            else await _api.CreateBudgetAsync(body);
            Finished?.Invoke();
        }
        catch (Exception ex) { ErrorMessage = "Não foi possível salvar. " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is not Guid id || IsBusy) return;
        IsBusy = true;
        try { await _api.DeleteBudgetAsync(id); Finished?.Invoke(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
