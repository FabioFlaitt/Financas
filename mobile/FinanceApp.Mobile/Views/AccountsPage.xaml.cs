using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _vm;

    public AccountsPage(AccountsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAddClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is AccountDto a)
            await OpenEditorAsync(a);
    }

    private async Task OpenEditorAsync(AccountDto? account)
    {
        var page = ServiceHelper.Get<AccountEditPage>();
        page.Initialize(account);
        await Navigation.PushAsync(page);
    }
}
