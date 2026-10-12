using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class TransactionEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _editingId;

    public TransactionEditViewModel(FinanceApiClient api)
    {
        _api = api;
        Types = new ObservableCollection<PickerOption<TransactionType>>
        {
            new(TransactionType.Expense, "Despesa"),
            new(TransactionType.Income, "Receita"),
            new(TransactionType.Transfer, "Transferência")
        };
        Statuses = new ObservableCollection<PickerOption<TransactionStatus>>
        {
            new(TransactionStatus.Confirmed, "Confirmado"),
            new(TransactionStatus.Pending, "Pendente"),
            new(TransactionStatus.Forecast, "Previsto")
        };
        SelectedType = Types[0];
        SelectedStatus = Statuses[0];
    }

    /// <summary>Disparado após salvar/excluir com sucesso para a página fechar.</summary>
    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Novo lançamento";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string? _merchant;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PickerOption<TransactionType>> Types { get; }
    public ObservableCollection<PickerOption<TransactionStatus>> Statuses { get; }
    public ObservableCollection<AccountDto> Accounts { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();

    [ObservableProperty] private PickerOption<TransactionType>? _selectedType;
    [ObservableProperty] private PickerOption<TransactionStatus>? _selectedStatus;
    [ObservableProperty] private AccountDto? _selectedAccount;
    [ObservableProperty] private CategoryDto? _selectedCategory;

    public async Task InitializeAsync(TransactionDto? tx)
    {
        try
        {
            var accounts = await _api.ListAccountsAsync();
            Accounts.Clear();
            foreach (var a in accounts) Accounts.Add(a);

            var categories = await _api.ListCategoriesAsync();
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }

        if (tx is not null)
        {
            _editingId = tx.Id;
            IsEditing = true;
            Title = "Editar lançamento";
            Description = tx.Description;
            AmountText = tx.Amount.ToString("0.00", BrazilCulture.PtBr);
            Date = tx.TransactionDate.ToDateTime(TimeOnly.MinValue);
            Merchant = tx.Merchant;
            Notes = tx.Notes;
            SelectedType = Types.FirstOrDefault(t => t.Value == tx.Type) ?? Types[0];
            SelectedStatus = Statuses.FirstOrDefault(s => s.Value == tx.Status) ?? Statuses[0];
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == tx.AccountId);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == tx.CategoryId);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Informe a descrição.";
            return;
        }
        if (!TryParseAmount(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Informe um valor válido maior que zero.";
            return;
        }

        var body = new
        {
            description = Description.Trim(),
            amount,
            type = SelectedType!.Value,
            status = SelectedStatus!.Value,
            transactionDate = DateOnly.FromDateTime(Date),
            categoryId = SelectedCategory?.Id,
            accountId = SelectedAccount?.Id,
            merchant = Merchant,
            notes = Notes
        };

        IsBusy = true;
        try
        {
            if (_editingId is Guid id)
                await _api.UpdateTransactionAsync(id, body);
            else
                await _api.CreateTransactionAsync(body);
            Finished?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível salvar. " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingId is not Guid id || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.DeleteTransactionAsync(id);
            Finished?.Invoke();
        }
        catch (Exception ex) { ErrorMessage = "Não foi possível excluir. " + ex.Message; }
        finally { IsBusy = false; }
    }

    private static bool TryParseAmount(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        // Aceita vírgula (pt-BR) ou ponto.
        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
