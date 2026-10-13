using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalFinanceStore _store;

    public SettingsPage(LocalFinanceStore store)
    {
        InitializeComponent();
        _store = store;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try { VersionLabel.Text = $"Versão {AppInfo.Current.VersionString}"; }
        catch { /* AppInfo pode não estar disponível em alguns ambientes */ }
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
}
