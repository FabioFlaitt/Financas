using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Encapsula o GPS do dispositivo (MAUI Geolocation). Pede permissão de localização,
/// obtém a posição atual e transmite atualizações ao vivo enquanto o app está em primeiro plano.
/// </summary>
public class LocationService
{
    private bool _listening;

    public event EventHandler<Location>? LocationChanged;

    public async Task<bool> EnsurePermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Location?> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await EnsurePermissionAsync()) return null;
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            return await Geolocation.Default.GetLocationAsync(request, ct)
                   ?? await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> StartAsync()
    {
        if (_listening) return true;
        if (!await EnsurePermissionAsync()) return false;

        try
        {
            Geolocation.Default.LocationChanged += OnLocationChanged;
            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
            var started = await Geolocation.Default.StartListeningForegroundAsync(request);
            _listening = started;
            return started;
        }
        catch
        {
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            return false;
        }
    }

    public void Stop()
    {
        if (!_listening) return;
        try { Geolocation.Default.StopListeningForeground(); } catch { }
        Geolocation.Default.LocationChanged -= OnLocationChanged;
        _listening = false;
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        => LocationChanged?.Invoke(this, e.Location);
}
