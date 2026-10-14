using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.Views;

/// <summary>
/// Tela de bloqueio: pede a biometria (Face ID/Touch ID no iOS) ao abrir e ao voltar do segundo plano.
/// Só libera (<see cref="Unlocked"/>) após a autenticação. Bloqueia o botão voltar.
/// </summary>
public sealed class LockPage : ContentPage
{
    private readonly BiometricLockService _lock;

    public event Action? Unlocked;

    public LockPage(BiometricLockService lockService)
    {
        _lock = lockService;
        BackgroundColor = Color.FromArgb("#0D47A1");

        var button = new Button
        {
            Text = "Desbloquear",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#0D47A1"),
            CornerRadius = 12,
            Padding = new Thickness(24, 10)
        };
        button.Clicked += async (_, _) => await TryAuthAsync();

        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 18,
            Padding = 24,
            Children =
            {
                new Label { Text = "🔒", FontSize = 56, HorizontalOptions = LayoutOptions.Center },
                new Label { Text = "Flaitt Finanças", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center },
                new Label { Text = "Use a biometria para abrir", FontSize = 13, TextColor = Color.FromArgb("#BBD4F5"), HorizontalOptions = LayoutOptions.Center },
                button
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TryAuthAsync();
    }

    private async Task TryAuthAsync()
    {
        if (await _lock.AuthenticateAsync())
            Unlocked?.Invoke();
    }

    // Impede fechar a trava pelo botão voltar do Android.
    protected override bool OnBackButtonPressed() => true;
}
