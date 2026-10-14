using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.Views;

namespace FinanceApp.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly LocalFinanceStore _store;
    private readonly BiometricLockService _lock;
    private bool _lockShown;
    private bool _wasBackgrounded;

    public App(LocalFinanceStore store, BiometricLockService lockService)
    {
        InitializeComponent();
        _store = store;
        _lock = lockService;
        // Splash provisória enquanto decidimos entre Boas-vindas e AppShell.
        MainPage = BuildLoadingPage();
        _ = InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Stopped += (_, _) => { if (_lock.IsEnabled) _wasBackgrounded = true; };
        window.Resumed += async (_, _) =>
        {
            if (_lock.IsEnabled && _wasBackgrounded)
            {
                _wasBackgrounded = false;
                await ShowLockModalAsync();
            }
        };
        return window;
    }

    private async Task InitializeAsync()
    {
        try
        {
            var hasProfile = await _store.HasProfileAsync();
            Page target = hasProfile
                ? new AppShell()
                : new NavigationPage(ServiceHelper.Get<WelcomePage>()) { BarBackgroundColor = Color.FromArgb("#0D47A1") };

            var lockAtStart = hasProfile && _lock.IsEnabled && await _lock.IsAvailableAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (lockAtStart)
                {
                    // Abre travado: a tela de bloqueio troca para o conteúdo real ao autenticar.
                    var lockPage = new LockPage(_lock);
                    lockPage.Unlocked += () => MainThread.BeginInvokeOnMainThread(() => MainPage = target);
                    MainPage = lockPage;
                }
                else
                {
                    MainPage = target;
                }
            });
        }
        catch (Exception ex)
        {
            // Em vez de "entrar com erro silencioso", mostra a mensagem para o usuário poder relatar.
            await MainThread.InvokeOnMainThreadAsync(() => MainPage = BuildErrorPage(ex));
        }
    }

    /// <summary>Cobre o conteúdo atual com a trava ao voltar do segundo plano (preserva a navegação).</summary>
    private async Task ShowLockModalAsync()
    {
        if (_lockShown || MainPage is null) return;
        _lockShown = true;

        var lockPage = new LockPage(_lock);
        lockPage.Unlocked += () => MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await MainPage!.Navigation.PopModalAsync(false); } catch { }
            _lockShown = false;
        });

        try { await MainPage.Navigation.PushModalAsync(lockPage, false); }
        catch { _lockShown = false; }
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
