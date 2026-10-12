using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class TripsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public TripsViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<TripDto> Trips { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasTrips;
    [ObservableProperty] private bool _isDemoOrOffline;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var trips = await _api.ListTripsAsync();
            Trips.Clear();
            foreach (var t in trips) Trips.Add(t);
            HasTrips = Trips.Count > 0;
            IsDemoOrOffline = false;
        }
        catch
        {
            // Sem backend: exibe viagens de demonstração claramente identificadas.
            Trips.Clear();
            if (ApiConfig.FallbackToDemoData)
            {
                foreach (var t in DemoTripsData.Build()) Trips.Add(t);
            }
            HasTrips = Trips.Count > 0;
            IsDemoOrOffline = true;
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> DeleteAsync(TripDto trip)
    {
        try
        {
            await _api.DeleteTripAsync(trip.Id);
            Trips.Remove(trip);
            HasTrips = Trips.Count > 0;
            return true;
        }
        catch { return false; }
    }
}
