using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class RecurringPage : ContentPage
{
    private readonly RecurringViewModel _vm;

    public RecurringPage(RecurringViewModel vm)
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
        if (sender is BindableObject b && b.BindingContext is RecurringTransactionDto r)
            await OpenEditorAsync(r);
    }

    private async Task OpenEditorAsync(RecurringTransactionDto? recurring)
    {
        var page = ServiceHelper.Get<RecurringEditPage>();
        page.Initialize(recurring);
        await Navigation.PushAsync(page);
    }
}
