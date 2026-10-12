using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class AccountEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _id;
    public AccountEditViewModel(FinanceApiClient api)
    {
        _api = api;
        Types = new ObservableCollection<PickerOption<AccountType>>
        {
            new(AccountType.Checking, "Conta corrente"),
            new(AccountType.Savings, "Poupança"),
            new(AccountType.Wallet, "Carteira"),
            new(AccountType.Investment, "Investimento"),
            new(AccountType.Other, "Outro")
        };
        SelectedType = Types[0];
    }

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Nova conta";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _institution;
    [ObservableProperty] private string _initialBalanceText = "0,00";
    [ObservableProperty] private bool _includeInTotal = true;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PickerOption<AccountType>> Types { get; }
    [ObservableProperty] private PickerOption<AccountType>? _selectedType;

    public void Initialize(AccountDto? a)
    {
        if (a is null) return;
        _id = a.Id;
        IsEditing = true;
        Title = "Editar conta";
        Name = a.Name;
        Institution = a.Institution;
        InitialBalanceText = a.InitialBalance.ToString("0.00", BrazilCulture.PtBr);
        IncludeInTotal = a.IncludeInTotalBalance;
        SelectedType = Types.FirstOrDefault(t => t.Value == a.Type) ?? Types[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Informe o nome da conta."; return; }
        if (!MoneyParser.TryParse(InitialBalanceText, out var balance)) { ErrorMessage = "Saldo inicial inválido."; return; }

        var body = new
        {
            name = Name.Trim(),
            institution = Institution,
            type = SelectedType!.Value,
            initialBalance = balance,
            includeInTotalBalance = IncludeInTotal
        };
        IsBusy = true;
        try
        {
            if (_id is Guid id) await _api.UpdateAccountAsync(id, body);
            else await _api.CreateAccountAsync(body);
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
        try { await _api.DeleteAccountAsync(id); Finished?.Invoke(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}

public partial class CategoryEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _id;
    public CategoryEditViewModel(FinanceApiClient api)
    {
        _api = api;
        Natures = new ObservableCollection<PickerOption<ExpenseNature>>
        {
            new(ExpenseNature.Variable, "Variável"),
            new(ExpenseNature.Fixed, "Fixa"),
            new(ExpenseNature.NotApplicable, "Não se aplica")
        };
        SelectedNature = Natures[0];
    }

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Nova categoria";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _colorHex = "#607D8B";
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<string> Palette { get; } = new()
    {
        "#5E35B1", "#2E7D32", "#EF6C00", "#1565C0", "#C62828", "#AD1457",
        "#6A1B9A", "#00838F", "#283593", "#00695C", "#455A64", "#757575"
    };

    public ObservableCollection<PickerOption<ExpenseNature>> Natures { get; }
    [ObservableProperty] private PickerOption<ExpenseNature>? _selectedNature;

    public void Initialize(CategoryDto? c)
    {
        if (c is null) return;
        _id = c.Id;
        IsEditing = true;
        Title = "Editar categoria";
        Name = c.Name;
        ColorHex = c.ColorHex;
        SelectedNature = Natures.FirstOrDefault(n => n.Value == c.DefaultNature) ?? Natures[0];
    }

    [RelayCommand]
    private void PickColor(string hex) => ColorHex = hex;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Informe o nome da categoria."; return; }

        var body = new { name = Name.Trim(), colorHex = ColorHex, icon = "tag", defaultNature = SelectedNature!.Value };
        IsBusy = true;
        try
        {
            if (_id is Guid id) await _api.UpdateCategoryAsync(id, body);
            else await _api.CreateCategoryAsync(body);
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
        try { await _api.DeleteCategoryAsync(id); Finished?.Invoke(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}

public partial class RecurringEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _id;
    public RecurringEditViewModel(FinanceApiClient api)
    {
        _api = api;
        Types = new ObservableCollection<PickerOption<TransactionType>>
        {
            new(TransactionType.Expense, "Despesa"),
            new(TransactionType.Income, "Receita")
        };
        Frequencies = new ObservableCollection<PickerOption<RecurrenceFrequency>>
        {
            new(RecurrenceFrequency.Monthly, "Mensal"),
            new(RecurrenceFrequency.Weekly, "Semanal"),
            new(RecurrenceFrequency.Biweekly, "Quinzenal"),
            new(RecurrenceFrequency.Bimonthly, "Bimestral"),
            new(RecurrenceFrequency.Quarterly, "Trimestral"),
            new(RecurrenceFrequency.Semiannual, "Semestral"),
            new(RecurrenceFrequency.Yearly, "Anual")
        };
        SelectedType = Types[0];
        SelectedFrequency = Frequencies[0];
    }

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Nova recorrência";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PickerOption<TransactionType>> Types { get; }
    public ObservableCollection<PickerOption<RecurrenceFrequency>> Frequencies { get; }
    [ObservableProperty] private PickerOption<TransactionType>? _selectedType;
    [ObservableProperty] private PickerOption<RecurrenceFrequency>? _selectedFrequency;

    public void Initialize(RecurringTransactionDto? r)
    {
        if (r is null) return;
        _id = r.Id;
        IsEditing = true;
        Title = "Editar recorrência";
        Description = r.Description;
        AmountText = r.Amount.ToString("0.00", BrazilCulture.PtBr);
        StartDate = r.StartDate.ToDateTime(TimeOnly.MinValue);
        IsActive = r.IsActive;
        SelectedType = Types.FirstOrDefault(t => t.Value == r.Type) ?? Types[0];
        SelectedFrequency = Frequencies.FirstOrDefault(f => f.Value == r.Frequency) ?? Frequencies[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Description)) { ErrorMessage = "Informe a descrição."; return; }
        if (!MoneyParser.TryParse(AmountText, out var amount) || amount <= 0) { ErrorMessage = "Valor inválido."; return; }

        var body = new
        {
            description = Description.Trim(),
            amount,
            type = SelectedType!.Value,
            frequency = SelectedFrequency!.Value,
            startDate = DateOnly.FromDateTime(StartDate),
            isActive = IsActive
        };
        IsBusy = true;
        try
        {
            if (_id is Guid id) await _api.UpdateRecurringAsync(id, body);
            else await _api.CreateRecurringAsync(body);
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
        try { await _api.DeleteRecurringAsync(id); Finished?.Invoke(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}

internal static class MoneyParser
{
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
