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
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _showingDeleted;

    public string Title => ShowingDeleted ? "Viagens excluídas" : "Viagens";

    partial void OnShowingDeletedChanged(bool value) => OnPropertyChanged(nameof(Title));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var trips = ShowingDeleted ? await _api.ListDeletedTripsAsync() : await _api.ListTripsAsync();
            Trips.Clear();
            foreach (var t in trips) Trips.Add(t);
            HasTrips = Trips.Count > 0;
            HasError = false;
        }
        catch (Exception ex)
        {
            Trips.Clear();
            HasTrips = false;
            ErrorMessage = "Não foi possível carregar as viagens: " + ex.Message;
            HasError = true;
        }
        finally { IsBusy = false; }
    }

    public async Task ToggleDeletedViewAsync()
    {
        ShowingDeleted = !ShowingDeleted;
        await LoadAsync();
    }

    /// <summary>Exclusão lógica: some da lista mas continua no banco (recuperável em "Excluídas").</summary>
    public async Task<bool> SoftDeleteAsync(TripDto trip)
    {
        try { await _api.SoftDeleteTripAsync(trip.Id); Trips.Remove(trip); HasTrips = Trips.Count > 0; return true; }
        catch { return false; }
    }

    /// <summary>Exclusão física: remove de vez do banco do aparelho (irreversível).</summary>
    public async Task<bool> HardDeleteAsync(TripDto trip)
    {
        try { await _api.DeleteTripAsync(trip.Id); Trips.Remove(trip); HasTrips = Trips.Count > 0; return true; }
        catch { return false; }
    }

    public async Task<bool> RestoreAsync(TripDto trip)
    {
        try { await _api.RestoreTripAsync(trip.Id); Trips.Remove(trip); HasTrips = Trips.Count > 0; return true; }
        catch { return false; }
    }
}
