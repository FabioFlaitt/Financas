using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class AccountEditPage : ContentPage
{
    private readonly AccountEditViewModel _vm;

    public AccountEditPage(AccountEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += async () =>
        {
            if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync();
        };
    }

    public void Initialize(AccountDto? account) => _vm.Initialize(account);
}
