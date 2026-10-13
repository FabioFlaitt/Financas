using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalFinanceStore _store;
    private readonly AppSettingsService _settings;

    public SettingsPage(LocalFinanceStore store, AppSettingsService settings)
    {
        InitializeComponent();
        _store = store;
        _settings = settings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try { VersionLabel.Text = $"Versão {AppInfo.Current.VersionString}"; }
        catch { /* AppInfo pode não estar disponível em alguns ambientes */ }

        try { TollGuruKeyEntry.Text = await _settings.GetTollGuruApiKeyAsync(); }
        catch { /* ignora falha ao ler chave */ }
    }

    private async void OnSaveTollGuruKey(object? sender, EventArgs e)
    {
        await _settings.SetTollGuruApiKeyAsync(TollGuruKeyEntry.Text);
        await DisplayAlert("Integrações", "Chave salva.", "OK");
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
        try
        {
            var file = await _store.CreateBackupFileAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Backup Flaitt Finanças",
                File = new ShareFile(file)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Backup", "Não foi possível gerar o backup: " + ex.Message, "OK");
        }
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
