using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class BudgetPage : ContentPage
{
    private readonly BudgetViewModel _vm;

    public BudgetPage(BudgetViewModel vm)
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
        if (sender is BindableObject b && b.BindingContext is BudgetItem item)
            await OpenEditorAsync(item);
    }

    private async Task OpenEditorAsync(BudgetItem? item)
    {
        var page = ServiceHelper.Get<BudgetEditPage>();
        await page.InitializeAsync(item, _vm.Year, _vm.Month);
        await Navigation.PushAsync(page);
    }
}
