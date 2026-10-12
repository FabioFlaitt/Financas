using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class ChartsPage : ContentPage
{
    private readonly ChartsViewModel _vm;

    public ChartsPage(ChartsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
