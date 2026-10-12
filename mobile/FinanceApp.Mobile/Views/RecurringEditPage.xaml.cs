using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class RecurringEditPage : ContentPage
{
    private readonly RecurringEditViewModel _vm;

    public RecurringEditPage(RecurringEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += async () =>
        {
            if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync();
        };
    }

    public void Initialize(RecurringTransactionDto? recurring) => _vm.Initialize(recurring);
}
