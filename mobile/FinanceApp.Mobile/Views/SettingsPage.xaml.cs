using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SessionService _session;

    public SettingsPage(SessionService session)
    {
        InitializeComponent();
        _session = session;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UserLabel.Text = string.IsNullOrWhiteSpace(_session.UserName) ? "Demonstração" : _session.UserName;
        DemoBadge.IsVisible = !_session.IsAuthenticated;

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

    private async void OnTripsTapped(object? sender, EventArgs e) => await OpenAsync<TripsPage>();
}
