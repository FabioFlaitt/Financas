using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.ViewModels;

public partial class TripDetailViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private readonly LocationService _location;
    private Guid _tripId;
    private bool _posting;

    public TripDetailViewModel(FinanceApiClient api, LocationService location)
    {
        _api = api;
        _location = location;
        _location.LocationChanged += OnLocationChanged;
    }

    /// <summary>Página ouve para desenhar o ponto no mapa.</summary>
    public event Action<double, double>? RoutePointAdded;
    /// <summary>Página ouve para centralizar o mapa.</summary>
    public event Action<double, double>? RequestCenter;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isTracking;
    [ObservableProperty] private string _tripName = "Viagem";
    [ObservableProperty] private string _statusLabel = string.Empty;
    [ObservableProperty] private bool _isInProgress;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private string _distanceLabel = "0 km";
    [ObservableProperty] private string _costLabel = "R$ 0,00";
    [ObservableProperty] private string _costPerKmLabel = "R$ 0,00/km";
    [ObservableProperty] private string _tollLabel = "R$ 0,00";
    [ObservableProperty] private string _fuelLabel = "R$ 0,00";
    [ObservableProperty] private string _durationLabel = "0 h";

    public TripDetailDto? Trip { get; private set; }

    public async Task LoadAsync(Guid tripId)
    {
        _tripId = tripId;
        IsBusy = true;
        try
        {
            Trip = await _api.GetTripAsync(tripId);
            TripName = Trip.Name;
            StatusLabel = Trip.StatusLabel;
            IsInProgress = Trip.IsInProgress;
            ApplySummary(Trip.Summary);

            // Redesenha a rota existente
            foreach (var p in Trip.Route)
                RoutePointAdded?.Invoke(p.Latitude, p.Longitude);
            var last = Trip.Route.LastOrDefault();
            if (last is not null) RequestCenter?.Invoke(last.Latitude, last.Longitude);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task StartTrackingAsync()
    {
        if (IsTracking) return;
        var current = await _location.GetCurrentAsync();
        if (current is not null) RequestCenter?.Invoke(current.Latitude, current.Longitude);

        var ok = await _location.StartAsync();
        if (!ok)
        {
            ErrorMessage = "Permissão de localização necessária para rastrear a viagem.";
            return;
        }
        IsTracking = true;
    }

    public void StopTracking()
    {
        _location.Stop();
        IsTracking = false;
    }

    public async Task<bool> AddTollAsync(decimal amount)
    {
        var loc = await _location.GetCurrentAsync();
        return await AddExpenseAsync(new
        {
            type = TripExpenseType.Toll,
            amount,
            latitude = loc?.Latitude,
            longitude = loc?.Longitude
        });
    }

    public async Task<bool> AddFuelAsync(decimal amount, decimal? liters)
    {
        var loc = await _location.GetCurrentAsync();
        return await AddExpenseAsync(new
        {
            type = TripExpenseType.Fuel,
            amount,
            liters,
            latitude = loc?.Latitude,
            longitude = loc?.Longitude
        });
    }

    private async Task<bool> AddExpenseAsync(object body)
    {
        try
        {
            await _api.AddTripExpenseAsync(_tripId, body);
            await ReloadSummaryAsync();
            return true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; return false; }
    }

    public async Task FinishAsync(decimal? endOdometer)
    {
        StopTracking();
        IsBusy = true;
        try
        {
            Trip = await _api.FinishTripAsync(_tripId, new { endOdometerKm = endOdometer });
            StatusLabel = Trip.StatusLabel;
            IsInProgress = Trip.IsInProgress;
            ApplySummary(Trip.Summary);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async void OnLocationChanged(object? sender, Location loc)
    {
        RoutePointAdded?.Invoke(loc.Latitude, loc.Longitude);
        RequestCenter?.Invoke(loc.Latitude, loc.Longitude);

        if (_posting) return; // evita chamadas sobrepostas
        _posting = true;
        try
        {
            var summary = await _api.AddTripLocationAsync(_tripId, new
            {
                latitude = loc.Latitude,
                longitude = loc.Longitude,
                speedKmh = loc.Speed is double s ? s * 3.6 : (double?)null,
                recordedAtUtc = DateTime.UtcNow
            });
            ApplySummary(summary);
        }
        catch { /* sem conexão: segue desenhando no mapa e tenta no próximo ponto */ }
        finally { _posting = false; }
    }

    private async Task ReloadSummaryAsync()
    {
        try
        {
            var trip = await _api.GetTripAsync(_tripId);
            Trip = trip;
            ApplySummary(trip.Summary);
        }
        catch { }
    }

    private void ApplySummary(TripSummary s)
    {
        DistanceLabel = s.DistanceKm.ToString("0.0", BrazilCulture.PtBr) + " km";
        CostLabel = s.TotalCost.ToString("C2", BrazilCulture.PtBr);
        CostPerKmLabel = s.CostPerKm.ToString("C2", BrazilCulture.PtBr) + "/km";
        TollLabel = $"{s.TollTotal.ToString("C2", BrazilCulture.PtBr)} ({s.TollCount})";
        FuelLabel = s.FuelLiters > 0
            ? $"{s.FuelTotal.ToString("C2", BrazilCulture.PtBr)} • {s.FuelLiters.ToString("0.0", BrazilCulture.PtBr)} L"
            : s.FuelTotal.ToString("C2", BrazilCulture.PtBr);
        DurationLabel = s.DurationHours >= 1
            ? s.DurationHours.ToString("0.0", BrazilCulture.PtBr) + " h"
            : (s.DurationHours * 60).ToString("0", BrazilCulture.PtBr) + " min";
    }
}
