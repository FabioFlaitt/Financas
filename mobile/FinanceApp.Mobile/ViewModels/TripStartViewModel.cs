using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>
/// Tela de planejar a viagem no estilo de um app de navegação: origem = "Seu local" (GPS), destino por
/// busca com sugestões (Photon/OSM) ou tocando no mapa, botão de inverter, e prévia de distância/tempo (ETA).
/// </summary>
public partial class TripStartViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private readonly LocationService _location;
    private readonly IPlaceSearchService _places;
    private readonly IRoutingService _routing;
    private readonly SavedPlacesService _saved;

    private CancellationTokenSource? _searchCts;

    public TripStartViewModel(FinanceApiClient api, LocationService location, IPlaceSearchService places,
        IRoutingService routing, SavedPlacesService saved)
    {
        _api = api;
        _location = location;
        _places = places;
        _routing = routing;
        _saved = saved;
    }

    /// <summary>Disparado ao iniciar com o id da viagem criada.</summary>
    public event Action<Guid>? Started;
    /// <summary>Página ouve para desenhar a prévia da rota no mapa.</summary>
    public event Action<PlannedRoute>? RoutePreview;
    /// <summary>Página ouve para centralizar o mapa num ponto.</summary>
    public event Action<Location>? CenterMap;

    public ObservableCollection<PlaceResult> SearchResults { get; } = new();
    public ObservableCollection<SavedPlace> Suggestions { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private string _originLabel = "Seu local";
    private Location? _originLocation;

    [ObservableProperty] private string _destinationQuery = string.Empty;
    [ObservableProperty] private string? _destinationLabel;
    private Location? _destinationLocation;

    [ObservableProperty] private bool _hasRoute;
    [ObservableProperty] private string _distanceLabel = string.Empty;
    [ObservableProperty] private string _etaLabel = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _vehicleName;
    [ObservableProperty] private string? _startOdometerText;
    [ObservableProperty] private string? _fuelPriceText;

    public bool HasResults => SearchResults.Count > 0;
    public bool ShowSuggestions => SearchResults.Count == 0 && _destinationLocation is null;

    /// <summary>Pega a localização atual (origem) e carrega destinos recentes/favoritos.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            _originLocation = await _location.GetCurrentAsync();
            if (_originLocation is not null)
            {
                CenterMap?.Invoke(_originLocation);
                var address = await _places.ReverseAsync(_originLocation);
                if (!string.IsNullOrWhiteSpace(address)) OriginLabel = "Seu local • " + address;
            }
        }
        catch { /* sem GPS: segue, usuário ainda pode escolher pelo mapa */ }

        await ReloadSuggestionsAsync();
    }

    private async Task ReloadSuggestionsAsync()
    {
        Suggestions.Clear();
        foreach (var s in await _saved.GetSuggestionsAsync()) Suggestions.Add(s);
        OnPropertyChanged(nameof(ShowSuggestions));
    }

    partial void OnDestinationQueryChanged(string value) => _ = SearchDebouncedAsync(value);

    private async Task SearchDebouncedAsync(string query)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        try
        {
            await Task.Delay(350, cts.Token); // espera o usuário parar de digitar
            var results = await _places.SearchAsync(query, _originLocation, cts.Token);
            if (cts.IsCancellationRequested) return;

            SearchResults.Clear();
            foreach (var r in results) SearchResults.Add(r);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowSuggestions));
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task SelectResultAsync(PlaceResult place) => await SetDestinationAsync(place);

    [RelayCommand]
    private async Task SelectSuggestionAsync(SavedPlace place)
        => await SetDestinationAsync(new PlaceResult(place.Label, place.Latitude, place.Longitude));

    /// <summary>Chamado pelo code-behind quando o usuário toca no mapa para escolher o destino.</summary>
    public async Task SetDestinationFromMapAsync(Location location)
    {
        var label = await _places.ReverseAsync(location) ?? "Destino no mapa";
        await SetDestinationAsync(new PlaceResult(label, location.Latitude, location.Longitude));
    }

    private async Task SetDestinationAsync(PlaceResult place)
    {
        _destinationLocation = new Location(place.Latitude, place.Longitude);
        DestinationLabel = place.Label;
        DestinationQuery = string.Empty;
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowSuggestions));
        await _saved.AddRecentAsync(place);
        await ComputeRouteAsync();
    }

    [RelayCommand]
    private void ClearDestination()
    {
        _destinationLocation = null;
        DestinationLabel = null;
        HasRoute = false;
        DistanceLabel = EtaLabel = string.Empty;
        OnPropertyChanged(nameof(ShowSuggestions));
    }

    [RelayCommand]
    private async Task SwapAsync()
    {
        if (_destinationLocation is null) return;
        // Troca origem <-> destino (a origem passa a ser o destino escolhido e vice-versa).
        (_originLocation, _destinationLocation) = (_destinationLocation, _originLocation);
        var oldOrigin = OriginLabel;
        OriginLabel = DestinationLabel ?? "Origem";
        DestinationLabel = oldOrigin.Replace("Seu local • ", string.Empty);
        await ComputeRouteAsync();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(SavedPlace place)
    {
        await _saved.ToggleFavoriteAsync(place);
        await ReloadSuggestionsAsync();
    }

    private async Task ComputeRouteAsync()
    {
        if (_originLocation is null || _destinationLocation is null) return;
        try
        {
            var plan = await _routing.PlanAsync(_originLocation, _destinationLocation);
            if (plan is null) { HasRoute = false; return; }

            DistanceLabel = plan.DistanceKm.ToString("0.0", BrazilCulture.PtBr) + " km";
            EtaLabel = FormatDuration(plan.DurationMinutes);
            HasRoute = true;
            RoutePreview?.Invoke(plan);
        }
        catch { HasRoute = false; }
    }

    private static string FormatDuration(double minutes)
    {
        if (minutes < 60) return $"{minutes:0} min";
        var h = (int)(minutes / 60);
        var m = (int)(minutes % 60);
        return m > 0 ? $"{h} h {m} min" : $"{h} h";
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (_destinationLocation is null)
        {
            ErrorMessage = "Escolha um destino (busque ou toque no mapa).";
            return;
        }

        var origin = CleanLabel(OriginLabel);
        var destination = DestinationLabel ?? "Destino";
        var name = string.IsNullOrWhiteSpace(Name) ? $"{Short(origin)} → {Short(destination)}" : Name.Trim();

        IsBusy = true;
        try
        {
            var trip = await _api.StartTripAsync(new
            {
                name,
                origin,
                destination,
                originLatitude = _originLocation?.Latitude,
                originLongitude = _originLocation?.Longitude,
                destinationLatitude = _destinationLocation?.Latitude,
                destinationLongitude = _destinationLocation?.Longitude,
                vehicleName = VehicleName,
                startOdometerKm = ParseDecimal(StartOdometerText),
                fuelPricePerLiter = ParseDecimal(FuelPriceText)
            });
            Started?.Invoke(trip.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível iniciar a viagem. " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    private static string CleanLabel(string label) => label.Replace("Seu local • ", string.Empty).Trim();
    private static string Short(string label) => label.Split(',')[0].Trim();

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
