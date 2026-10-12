using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class ImportPage : ContentPage
{
    private readonly ImportViewModel _vm;

    public ImportPage(ImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
