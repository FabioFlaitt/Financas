using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.Views;

namespace FinanceApp.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly LocalFinanceStore _store;

    public App(LocalFinanceStore store)
    {
        InitializeComponent();
        _store = store;
        // Splash provisória enquanto decidimos entre Boas-vindas e AppShell.
        MainPage = BuildLoadingPage();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var hasProfile = await _store.HasProfileAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MainPage = hasProfile
                    ? new AppShell()
                    : new NavigationPage(ServiceHelper.Get<WelcomePage>()) { BarBackgroundColor = Color.FromArgb("#0D47A1") };
            });
        }
        catch
        {
            // Falha ao abrir o banco: ainda assim entrega as abas; a tela de erro aparece na primeira operação.
            await MainThread.InvokeOnMainThreadAsync(() => MainPage = new AppShell());
        }
    }

    private static ContentPage BuildLoadingPage() => new()
    {
        BackgroundColor = Color.FromArgb("#0D47A1"),
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16,
            Children =
            {
                new Label { Text = "Flaitt Finanças", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center },
                new ActivityIndicator { IsRunning = true, Color = Colors.White }
            }
        }
    };
}
