using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
#if IOS
using CoreLocation;
#endif

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Encapsula o GPS do dispositivo. No iOS usa o CLLocationManager nativo com atualizações em segundo
/// plano (necessário para detectar pedágios com a tela bloqueada); nas demais plataformas usa o
/// MAUI Geolocation em primeiro plano.
/// </summary>
public class LocationService
{
    private bool _listening;

    public event EventHandler<Location>? LocationChanged;

#if IOS
    private CLLocationManager? _clManager;
#endif

    public async Task<bool> EnsurePermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return false;

            // Tenta elevar para localização contínua (background) — necessário para detectar
            // pedágios com o celular bloqueado durante a viagem. Se o usuário não conceder,
            // o rastreio ainda funciona com o app em primeiro plano.
            try
            {
                var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                if (always != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.LocationAlways>();
            }
            catch { /* nem toda plataforma expõe LocationAlways; segue com WhenInUse */ }

            return true;
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

#if IOS
        return StartIosBackground();
#else
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
#endif
    }

    public void Stop()
    {
        if (!_listening) return;
#if IOS
        try { _clManager?.StopUpdatingLocation(); } catch { }
        _clManager = null;
#else
        try { Geolocation.Default.StopListeningForeground(); } catch { }
        Geolocation.Default.LocationChanged -= OnLocationChanged;
#endif
        _listening = false;
    }

#if IOS
    /// <summary>GPS nativo do iOS com atualização contínua em segundo plano (tela bloqueada).</summary>
    private bool StartIosBackground()
    {
        try
        {
            _clManager = new CLLocationManager
            {
                DesiredAccuracy = CLLocation.AccuracyBestForNavigation,
                DistanceFilter = 10,
                AllowsBackgroundLocationUpdates = true,
                PausesLocationUpdatesAutomatically = false,
                ShowsBackgroundLocationIndicator = true
            };
            _clManager.LocationsUpdated += (_, e) =>
            {
                var last = e.Locations.LastOrDefault();
                if (last is null) return;
                LocationChanged?.Invoke(this, new Location(last.Coordinate.Latitude, last.Coordinate.Longitude)
                {
                    Speed = last.Speed >= 0 ? last.Speed : null
                });
            };
            _clManager.RequestAlwaysAuthorization();
            _clManager.StartUpdatingLocation();
            _listening = true;
            return true;
        }
        catch
        {
            _clManager = null;
            return false;
        }
    }
#else
    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        => LocationChanged?.Invoke(this, e.Location);
#endif
}
