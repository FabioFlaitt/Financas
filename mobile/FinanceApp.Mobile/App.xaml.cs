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
        catch (Exception ex)
        {
            // Em vez de "entrar com erro silencioso", mostra a mensagem para o usuário poder relatar.
            await MainThread.InvokeOnMainThreadAsync(() => MainPage = BuildErrorPage(ex));
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

    private static ContentPage BuildErrorPage(Exception ex) => new()
    {
        BackgroundColor = Color.FromArgb("#0D47A1"),
        Padding = new Thickness(24),
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "⚠️", FontSize = 48, HorizontalOptions = LayoutOptions.Center },
                    new Label { Text = "Não foi possível abrir seus dados", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = ex.Message, FontSize = 13, TextColor = Color.FromArgb("#FFCDD2"), HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = ex.GetType().FullName ?? "", FontSize = 11, TextColor = Color.FromArgb("#BBD4F5"), HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        }
    };
}
