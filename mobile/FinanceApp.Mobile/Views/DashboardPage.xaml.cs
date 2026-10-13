using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Dashboard is null)
            await _vm.LoadAsync();
    }

    private async void OnVoiceTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(ServiceHelper.Get<VoiceExpensePage>());
        await _vm.LoadAsync(); // após voltar, atualiza os números
    }
}
