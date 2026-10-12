using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _vm;

    public TransactionsPage(TransactionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAddClicked(object? sender, EventArgs e)
        => await OpenEditorAsync(null);

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is TransactionDto tx)
            await OpenEditorAsync(tx);
    }

    private async Task OpenEditorAsync(TransactionDto? transaction)
    {
        var page = ServiceHelper.Get<TransactionEditPage>();
        await page.InitializeAsync(transaction);
        await Navigation.PushAsync(page);
    }
}
