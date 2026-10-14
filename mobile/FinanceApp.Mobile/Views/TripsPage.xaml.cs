using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class TripsPage : ContentPage
{
    private readonly TripsViewModel _vm;

    public TripsPage(TripsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var page = ServiceHelper.Get<TripStartPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnTripTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is TripDto trip)
        {
            var page = ServiceHelper.Get<TripDetailPage>();
            await page.InitializeAsync(trip.Id);
            await Navigation.PushAsync(page);
        }
    }

    private async void OnToggleDeleted(object? sender, EventArgs e)
        => await _vm.ToggleDeletedViewAsync();

    private async void OnTripMenu(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: TripDto trip }) return;

        if (_vm.ShowingDeleted)
        {
            var action = await DisplayActionSheet(trip.Name, "Cancelar", null, "Restaurar", "Excluir definitivo");
            if (action == "Restaurar") await _vm.RestoreAsync(trip);
            else if (action == "Excluir definitivo") await ConfirmHardDeleteAsync(trip);
            return;
        }

        var choice = await DisplayActionSheet(trip.Name, "Cancelar", null, "Excluir (recuperável)", "Excluir definitivo");
        if (choice == "Excluir (recuperável)")
            await _vm.SoftDeleteAsync(trip);
        else if (choice == "Excluir definitivo")
            await ConfirmHardDeleteAsync(trip);
    }

    private async Task ConfirmHardDeleteAsync(TripDto trip)
    {
        var ok = await DisplayAlert("Excluir definitivo",
            "Isto remove a viagem e seus gastos de vez deste aparelho. Não dá para desfazer. Continuar?",
            "Excluir", "Cancelar");
        if (ok) await _vm.HardDeleteAsync(trip);
    }
}
