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
}
