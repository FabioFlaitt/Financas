using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class WelcomePage : ContentPage
{
    private readonly WelcomeViewModel _vm;

    public WelcomePage(WelcomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += OnFinished;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.IsReady) await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _vm.Finished -= OnFinished;
        base.OnDisappearing();
    }

    private void OnFinished()
    {
        // Troca a página raiz para a casca com as abas; o welcome não volta mais.
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.MainPage = new AppShell();
    }
}
