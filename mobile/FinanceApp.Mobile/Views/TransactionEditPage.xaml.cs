using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class TransactionEditPage : ContentPage
{
    private readonly TransactionEditViewModel _vm;

    public TransactionEditPage(TransactionEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += OnFinished;
    }

    /// <summary>Carrega os dados; passe o lançamento para editar, ou null para criar.</summary>
    public async Task InitializeAsync(TransactionDto? transaction) => await _vm.InitializeAsync(transaction);

    private async void OnFinished()
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
