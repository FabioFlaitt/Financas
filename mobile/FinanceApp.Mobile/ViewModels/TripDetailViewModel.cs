using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.ViewModels;

public partial class TripDetailViewModel : ObservableObject
{
    private const double TollGeofenceKm = 0.15; // dispara o registro ao chegar a ~150 m do pedágio

    private readonly FinanceApiClient _api;
    private readonly LocationService _location;
    private readonly IRoutingService _routing;
    private Guid _tripId;
    private bool _posting;
    private readonly List<PlannedTollState> _plannedTolls = new();

    public TripDetailViewModel(FinanceApiClient api, LocationService location, IRoutingService routing)
    {
        _api = api;
        _location = location;
        _routing = routing;
        _location.LocationChanged += OnLocationChanged;
    }

    /// <summary>Pedágio previsto + se já foi registrado automaticamente nesta sessão.</summary>
    private sealed class PlannedTollState
    {
        public required PlannedToll Toll { get; init; }
        public bool Triggered { get; set; }
    }

    /// <summary>Despesas da viagem (pedágios/combustível) para a lista com editar/excluir.</summary>
    public ObservableCollection<TripExpenseDto> Expenses { get; } = new();

    /// <summary>Página ouve para desenhar o ponto no mapa.</summary>
    public event Action<double, double>? RoutePointAdded;
    /// <summary>Página ouve para centralizar o mapa.</summary>
    public event Action<double, double>? RequestCenter;
    /// <summary>Página ouve para desenhar a rota planejada e os pinos de pedágio.</summary>
    public event Action<PlannedRoute>? RoutePlanned;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isTracking;
    [ObservableProperty] private bool _hasExpenses;
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
            RefreshExpenses(Trip.Expenses);

            // Redesenha a rota existente
            foreach (var p in Trip.Route)
                RoutePointAdded?.Invoke(p.Latitude, p.Longitude);
            var last = Trip.Route.LastOrDefault();
            if (last is not null) RequestCenter?.Invoke(last.Latitude, last.Longitude);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }

        // Planeja a rota em segundo plano (não trava a tela se a API demorar).
        if (IsInProgress) _ = PlanRouteAsync();
    }

    /// <summary>
    /// Consulta o provedor de rotas (TollGuru) a partir da origem/destino da viagem, desenha a rota
    /// planejada no mapa e guarda os pedágios previstos (em memória) para o registro automático.
    /// </summary>
    private async Task PlanRouteAsync()
    {
        if (Trip is null) return;
        if (string.IsNullOrWhiteSpace(Trip.Origin) || string.IsNullOrWhiteSpace(Trip.Destination)) return;
        if (!await _routing.IsConfiguredAsync()) return;

        try
        {
            var origin = await GeocodeAsync(Trip.Origin!);
            var destination = await GeocodeAsync(Trip.Destination!);
            if (origin is null || destination is null) return;

            var plan = await _routing.PlanAsync(origin, destination);
            if (plan is null) return;

            _plannedTolls.Clear();
            foreach (var t in plan.Tolls)
                _plannedTolls.Add(new PlannedTollState { Toll = t });

            RoutePlanned?.Invoke(plan);
        }
        catch { /* planejamento é um bônus; falha não impede rastrear a viagem */ }
    }

    private static async Task<Location?> GeocodeAsync(string address)
    {
        try
        {
            var results = await Geocoding.Default.GetLocationsAsync(address);
            return results?.FirstOrDefault();
        }
        catch { return null; }
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

    /// <summary>
    /// A cada ponto de GPS, registra automaticamente (sem perguntar) os pedágios previstos a ~150 m.
    /// Marca cada um como disparado para não duplicar.
    /// </summary>
    private async Task CheckTollGeofencesAsync(Location loc)
    {
        if (_plannedTolls.Count == 0) return;

        foreach (var state in _plannedTolls)
        {
            if (state.Triggered) continue;
            var distance = FinanceApp.Application.Trips.TripService.HaversineKm(
                loc.Latitude, loc.Longitude, state.Toll.Latitude, state.Toll.Longitude);
            if (distance > TollGeofenceKm) continue;

            state.Triggered = true; // marca antes de aguardar, evita registro duplicado
            await AddExpenseAsync(new
            {
                type = TripExpenseType.Toll,
                amount = state.Toll.Price,
                description = state.Toll.Name,
                latitude = state.Toll.Latitude,
                longitude = state.Toll.Longitude
            });
        }
    }

    public async Task<bool> UpdateExpenseAsync(Guid expenseId, TripExpenseType type, string? description, decimal amount, decimal? liters)
    {
        if (amount <= 0) { ErrorMessage = "Informe um valor maior que zero."; return false; }
        try
        {
            await _api.UpdateTripExpenseAsync(_tripId, expenseId, new
            {
                type,
                description,
                amount,
                liters
            });
            await ReloadSummaryAsync();
            return true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; return false; }
    }

    public async Task<bool> RemoveExpenseAsync(Guid expenseId)
    {
        try
        {
            await _api.DeleteTripExpenseAsync(_tripId, expenseId);
            await ReloadSummaryAsync();
            return true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; return false; }
    }

    private void RefreshExpenses(IEnumerable<TripExpenseDto> expenses)
    {
        Expenses.Clear();
        foreach (var e in expenses.OrderByDescending(x => x.OccurredAtUtc))
            Expenses.Add(e);
        HasExpenses = Expenses.Count > 0;
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

        await CheckTollGeofencesAsync(loc);

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
            MainThread.BeginInvokeOnMainThread(() => RefreshExpenses(trip.Expenses));
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
