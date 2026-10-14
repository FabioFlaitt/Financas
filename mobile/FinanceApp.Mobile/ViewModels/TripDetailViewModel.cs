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
    private const double TollGeofenceKm = 0.15;     // dispara o registro ao chegar a ~150 m do pedágio
    private const double TollPreAlertKm = 0.6;       // avisa por voz ao chegar a ~600 m do pedágio
    private const double TurnAnnounceKm = 0.2;       // fala a manobra ao chegar a ~200 m dela
    private const double OffRouteKm = 0.07;          // considera "fora da rota" além de ~70 m do traçado
    private const int OffRoutePointsToReRoute = 3;   // pontos seguidos fora da rota antes de recalcular
    private const double MinSecondsBetweenReRoutes = 15;

    private readonly FinanceApiClient _api;
    private readonly LocationService _location;
    private readonly IRoutingService _routing;
    private readonly VoiceGuideService _voice;
    private Guid _tripId;
    private bool _posting;
    private readonly List<PlannedTollState> _plannedTolls = new();
    private readonly List<NavStep> _navSteps = new();
    private int _nextStepIndex;
    private readonly List<Location> _plannedPath = new();
    private Location? _destination;
    private bool _replanning;
    private int _offRouteCount;
    private DateTime _lastReRouteUtc = DateTime.MinValue;

    public TripDetailViewModel(FinanceApiClient api, LocationService location, IRoutingService routing, VoiceGuideService voice)
    {
        _api = api;
        _location = location;
        _routing = routing;
        _voice = voice;
        _location.LocationChanged += OnLocationChanged;
    }

    /// <summary>Pedágio previsto + se já foi avisado/registrado automaticamente nesta sessão.</summary>
    private sealed class PlannedTollState
    {
        public required PlannedToll Toll { get; init; }
        public bool PreAnnounced { get; set; }
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
    [ObservableProperty] private bool _voiceEnabled = true;
    [ObservableProperty] private string? _currentInstruction;
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

        try
        {
            // Prefere as coordenadas salvas na viagem (sem geocodificar de novo); cai para o texto se faltar.
            var origin = Trip.OriginLatitude is double olat && Trip.OriginLongitude is double olng
                ? new Location(olat, olng)
                : string.IsNullOrWhiteSpace(Trip.Origin) ? null : await GeocodeAsync(Trip.Origin!);
            var destination = Trip.DestinationLatitude is double dlat && Trip.DestinationLongitude is double dlng
                ? new Location(dlat, dlng)
                : string.IsNullOrWhiteSpace(Trip.Destination) ? null : await GeocodeAsync(Trip.Destination!);
            if (origin is null || destination is null) return;

            _destination = destination;
            await _voice.EnsureLocaleAsync();
            await ApplyPlanAsync(origin, destination);
        }
        catch { /* planejamento é um bônus; falha não impede rastrear a viagem */ }
    }

    /// <summary>Consulta a rota e atualiza traçado, pedágios e manobras. Usado no plano inicial e no recálculo.</summary>
    private async Task ApplyPlanAsync(Location origin, Location destination)
    {
        var plan = await _routing.PlanAsync(origin, destination);
        if (plan is null) return;

        _plannedTolls.Clear();
        foreach (var t in plan.Tolls)
        {
            // Se já existe um pedágio registrado perto deste ponto (ex.: app reiniciou no meio da viagem),
            // marca como disparado para não registrar de novo.
            var already = Trip?.Expenses.Any(e => e.Type == TripExpenseType.Toll
                && e.Latitude is double la && e.Longitude is double lo
                && FinanceApp.Application.Trips.TripService.HaversineKm(la, lo, t.Latitude, t.Longitude) <= TollGeofenceKm) ?? false;
            _plannedTolls.Add(new PlannedTollState { Toll = t, Triggered = already });
        }

        _navSteps.Clear();
        _navSteps.AddRange(plan.Steps);
        _nextStepIndex = 0;

        _plannedPath.Clear();
        _plannedPath.AddRange(plan.Path);
        _offRouteCount = 0;

        RoutePlanned?.Invoke(plan);
    }

    /// <summary>Recalcula a rota a partir da posição atual quando o motorista sai do traçado por alguns pontos.</summary>
    private async Task CheckReRouteAsync(Location loc)
    {
        if (_destination is null || _plannedPath.Count < 2 || _replanning) return;

        if (MinDistanceToPathKm(loc) <= OffRouteKm) { _offRouteCount = 0; return; }

        _offRouteCount++;
        if (_offRouteCount < OffRoutePointsToReRoute) return;
        if ((DateTime.UtcNow - _lastReRouteUtc).TotalSeconds < MinSecondsBetweenReRoutes) return;

        _replanning = true;
        _lastReRouteUtc = DateTime.UtcNow;
        try
        {
            if (VoiceEnabled) _voice.Speak("Recalculando a rota.");
            await ApplyPlanAsync(loc, _destination);
        }
        catch { /* mantém a rota anterior se o recálculo falhar */ }
        finally { _replanning = false; }
    }

    private double MinDistanceToPathKm(Location loc)
    {
        double min = double.MaxValue;
        foreach (var p in _plannedPath)
        {
            var km = FinanceApp.Application.Trips.TripService.HaversineKm(loc.Latitude, loc.Longitude, p.Latitude, p.Longitude);
            if (km < min) min = km;
        }
        return min;
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
        _voice.Stop();
        IsTracking = false;
    }

    /// <summary>Liga/desliga a narração por voz. Ao desligar, interrompe a fala atual.</summary>
    public void ToggleVoice()
    {
        VoiceEnabled = !VoiceEnabled;
        if (!VoiceEnabled) _voice.Stop();
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

    /// <summary>Fala a próxima manobra da rota (ex.: "Vire à direita") ao se aproximar dela.</summary>
    private void AnnounceNextTurn(Location loc)
    {
        if (!VoiceEnabled || _nextStepIndex >= _navSteps.Count) return;

        var step = _navSteps[_nextStepIndex];
        var distance = FinanceApp.Application.Trips.TripService.HaversineKm(
            loc.Latitude, loc.Longitude, step.Latitude, step.Longitude);
        if (distance > TurnAnnounceKm) return;

        _nextStepIndex++;
        CurrentInstruction = step.Instruction;
        _voice.Speak(step.Instruction);
    }

    /// <summary>
    /// A cada ponto de GPS, avisa por voz a aproximação do pedágio e o registra automaticamente
    /// (sem perguntar) ao chegar a ~150 m. Marca cada um para não repetir.
    /// </summary>
    private async Task CheckTollGeofencesAsync(Location loc)
    {
        if (_plannedTolls.Count == 0) return;

        foreach (var state in _plannedTolls)
        {
            if (state.Triggered) continue;
            var distance = FinanceApp.Application.Trips.TripService.HaversineKm(
                loc.Latitude, loc.Longitude, state.Toll.Latitude, state.Toll.Longitude);

            if (!state.PreAnnounced && distance <= TollPreAlertKm)
            {
                state.PreAnnounced = true;
                if (VoiceEnabled)
                {
                    var valor = state.Toll.Price > 0
                        ? $", valor {state.Toll.Price.ToString("C2", BrazilCulture.PtBr)}"
                        : string.Empty;
                    _voice.Speak($"Pedágio próximo{valor}.");
                }
            }

            if (distance > TollGeofenceKm) continue;

            state.Triggered = true; // marca antes de aguardar, evita registro duplicado
            var ok = await AddExpenseAsync(new
            {
                type = TripExpenseType.Toll,
                amount = state.Toll.Price,
                description = state.Toll.Name,
                latitude = state.Toll.Latitude,
                longitude = state.Toll.Longitude
            });
            if (ok && VoiceEnabled)
            {
                var valor = state.Toll.Price > 0
                    ? $" de {state.Toll.Price.ToString("C2", BrazilCulture.PtBr)}"
                    : string.Empty;
                _voice.Speak($"Pedágio{valor} registrado.");
            }
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

        await CheckReRouteAsync(loc);
        AnnounceNextTurn(loc);
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
