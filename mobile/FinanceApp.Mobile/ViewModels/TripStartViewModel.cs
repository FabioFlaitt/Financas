using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class TripStartViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;

    public TripStartViewModel(FinanceApiClient api) => _api = api;

    /// <summary>Disparado ao iniciar com o id da viagem criada.</summary>
    public event Action<Guid>? Started;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _origin;
    [ObservableProperty] private string? _destination;
    [ObservableProperty] private string? _vehicleName;
    [ObservableProperty] private string? _startOdometerText;
    [ObservableProperty] private string? _fuelPriceText;
    [ObservableProperty] private string? _errorMessage;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Dê um nome à viagem (ex.: “Férias no litoral”).";
            return;
        }

        IsBusy = true;
        try
        {
            var trip = await _api.StartTripAsync(new
            {
                name = Name.Trim(),
                origin = Origin,
                destination = Destination,
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

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
