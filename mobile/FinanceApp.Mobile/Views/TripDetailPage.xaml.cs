using System.Globalization;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace FinanceApp.Mobile.Views;

public partial class TripDetailPage : ContentPage
{
    private readonly TripDetailViewModel _vm;
    private Polyline? _route;
    private bool _centeredOnce;

    public TripDetailPage(TripDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.RoutePointAdded += OnRoutePointAdded;
        _vm.RequestCenter += OnRequestCenter;
    }

    public async Task InitializeAsync(Guid tripId) => await _vm.LoadAsync(tripId);

    private void OnRoutePointAdded(double lat, double lng)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _route ??= CreateRoute();
            _route.Geopath.Add(new Location(lat, lng));
        });
    }

    private void OnRequestCenter(double lat, double lng)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var radius = _centeredOnce ? Distance.FromKilometers(1) : Distance.FromKilometers(2);
            TripMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(lat, lng), radius));
            _centeredOnce = true;
        });
    }

    private Polyline CreateRoute()
    {
        var line = new Polyline { StrokeColor = Color.FromArgb("#0D47A1"), StrokeWidth = 6 };
        TripMap.MapElements.Add(line);
        return line;
    }

    private async void OnToggleTracking(object? sender, EventArgs e)
    {
        if (_vm.IsTracking)
        {
            _vm.StopTracking();
            TrackButton.Text = "▶ Rastrear rota (GPS)";
            TrackButton.BackgroundColor = Color.FromArgb("#1B5E20");
        }
        else
        {
            await _vm.StartTrackingAsync();
            if (_vm.IsTracking)
            {
                TrackButton.Text = "⏸ Rastreando… toque para pausar";
                TrackButton.BackgroundColor = Color.FromArgb("#C62828");
            }
        }
    }

    private async void OnAddToll(object? sender, EventArgs e)
    {
        var input = await DisplayPromptAsync("Pedágio", "Valor do pedágio (R$):", "Registrar", "Cancelar",
            placeholder: "0,00", keyboard: Keyboard.Numeric);
        if (TryParse(input, out var amount) && amount > 0)
            await _vm.AddTollAsync(amount);
    }

    private async void OnAddFuel(object? sender, EventArgs e)
    {
        var amountText = await DisplayPromptAsync("Combustível", "Valor abastecido (R$):", "Próximo", "Cancelar",
            placeholder: "0,00", keyboard: Keyboard.Numeric);
        if (!TryParse(amountText, out var amount) || amount <= 0) return;

        var litersText = await DisplayPromptAsync("Combustível", "Litros (opcional):", "Registrar", "Pular",
            placeholder: "Ex.: 40", keyboard: Keyboard.Numeric);
        decimal? liters = TryParse(litersText, out var l) && l > 0 ? l : null;

        await _vm.AddFuelAsync(amount, liters);
    }

    private async void OnFinish(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Encerrar viagem", "Deseja encerrar esta viagem?", "Encerrar", "Voltar");
        if (!confirm) return;

        var odometerText = await DisplayPromptAsync("Odômetro final", "Km final (opcional):", "Encerrar", "Pular",
            keyboard: Keyboard.Numeric);
        decimal? endOdometer = TryParse(odometerText, out var o) && o > 0 ? o : null;

        await _vm.FinishAsync(endOdometer);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopTracking();
    }

    private static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
