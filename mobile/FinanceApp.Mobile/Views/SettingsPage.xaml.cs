using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalFinanceStore _store;
    private readonly BackupReminderService _backup;
    private readonly BiometricLockService _lock;
    private bool _syncingSwitch;

    public SettingsPage(LocalFinanceStore store, BackupReminderService backup, BiometricLockService lockService)
    {
        InitializeComponent();
        _store = store;
        _backup = backup;
        _lock = lockService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try { VersionLabel.Text = $"Versão {AppInfo.Current.VersionString}"; }
        catch { /* AppInfo pode não estar disponível em alguns ambientes */ }

        _syncingSwitch = true;
        BiometricSwitch.IsToggled = _lock.IsEnabled;
        _syncingSwitch = false;
    }

    private async void OnBiometricToggled(object? sender, ToggledEventArgs e)
    {
        if (_syncingSwitch) return;

        if (e.Value)
        {
            if (!await _lock.IsAvailableAsync())
            {
                await DisplayAlert("Biometria", "Este aparelho não tem biometria configurada.", "OK");
                _syncingSwitch = true; BiometricSwitch.IsToggled = false; _syncingSwitch = false;
                return;
            }
            // Confirma com uma autenticação antes de ativar.
            if (!await _lock.AuthenticateAsync())
            {
                _syncingSwitch = true; BiometricSwitch.IsToggled = false; _syncingSwitch = false;
                return;
            }
        }

        _lock.IsEnabled = e.Value;
    }

    private Task OpenAsync<TPage>() where TPage : Page
        => Navigation.PushAsync(ServiceHelper.Get<TPage>());

    private async void OnAccountsTapped(object? sender, EventArgs e) => await OpenAsync<AccountsPage>();

    private async void OnCategoriesTapped(object? sender, EventArgs e) => await OpenAsync<CategoriesPage>();

    private async void OnRecurringTapped(object? sender, EventArgs e) => await OpenAsync<RecurringPage>();

    private async void OnBudgetTapped(object? sender, EventArgs e) => await OpenAsync<BudgetPage>();

    private async void OnImportTapped(object? sender, EventArgs e) => await OpenAsync<ImportPage>();

    private async void OnNotificationsTapped(object? sender, EventArgs e) => await OpenAsync<NotificationsPage>();

    private async void OnTripsTapped(object? sender, EventArgs e) => await OpenAsync<TripsPage>();

    private async void OnBackupTapped(object? sender, EventArgs e)
    {
        if (!await _backup.BackupNowAsync())
            await DisplayAlert("Backup", "Não foi possível gerar o backup.", "OK");
    }

    private async void OnResetTapped(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Começar do zero",
            "Isto vai apagar seu perfil, lançamentos e cartões deste aparelho. Não dá para desfazer. Continuar?",
            "Apagar tudo", "Cancelar");
        if (!confirm) return;

        try
        {
            await _store.ResetAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Começar do zero", "Falha ao apagar: " + ex.Message, "OK");
            return;
        }

        // Volta à tela de boas-vindas para criar um novo perfil.
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.MainPage = new NavigationPage(ServiceHelper.Get<Views.WelcomePage>())
            {
                BarBackgroundColor = Color.FromArgb("#0D47A1")
            };
    }
}
