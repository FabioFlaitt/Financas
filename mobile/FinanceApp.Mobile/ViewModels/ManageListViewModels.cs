using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    public AccountsViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<AccountDto> Items { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _totalLabel = "R$ 0,00";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _api.ListAccountsAsync();
            Items.Clear();
            foreach (var a in list) Items.Add(a);
            HasItems = Items.Count > 0;
            var total = Items.Where(a => a.IncludeInTotalBalance).Sum(a => a.CurrentBalance);
            TotalLabel = total.ToString("C2", BrazilCulture.PtBr);
        }
        catch { HasItems = false; }
        finally { IsBusy = false; }
    }

    public async Task<bool> DeleteAsync(AccountDto a)
    {
        try { await _api.DeleteAccountAsync(a.Id); await LoadAsync(); return true; }
        catch { return false; }
    }
}

public partial class CategoriesViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    public CategoriesViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<CategoryDto> Items { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _api.ListCategoriesAsync();
            Items.Clear();
            foreach (var c in list) Items.Add(c);
            HasItems = Items.Count > 0;
        }
        catch { HasItems = false; }
        finally { IsBusy = false; }
    }

    public async Task<bool> DeleteAsync(CategoryDto c)
    {
        try { await _api.DeleteCategoryAsync(c.Id); await LoadAsync(); return true; }
        catch { return false; }
    }
}

public partial class RecurringViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    public RecurringViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<RecurringTransactionDto> Items { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _api.ListRecurringAsync();
            Items.Clear();
            foreach (var r in list) Items.Add(r);
            HasItems = Items.Count > 0;
        }
        catch { HasItems = false; }
        finally { IsBusy = false; }
    }

    public async Task<bool> DeleteAsync(RecurringTransactionDto r)
    {
        try { await _api.DeleteRecurringAsync(r.Id); await LoadAsync(); return true; }
        catch { return false; }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        try { await _api.GenerateRecurringAsync(); }
        catch { /* ignora falhas de rede */ }
        await LoadAsync();
    }
}
