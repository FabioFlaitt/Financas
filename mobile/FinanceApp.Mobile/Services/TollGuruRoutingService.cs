using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Implementação de <see cref="IRoutingService"/> usando a API Origin-Destination da TollGuru.
/// A chave de API fica no <see cref="AppSettingsService"/> (armazenamento seguro). Falhas de rede
/// devolvem null: o app segue funcionando sem a rota planejada.
/// </summary>
public sealed class TollGuruRoutingService : IRoutingService
{
    private const string Endpoint = "https://apis.tollguru.com/toll/v2/origin-destination-waypoints";
    private readonly AppSettingsService _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public TollGuruRoutingService(AppSettingsService settings) => _settings = settings;

    public async Task<bool> IsConfiguredAsync()
        => !string.IsNullOrWhiteSpace(await _settings.GetTollGuruApiKeyAsync());

    public async Task<PlannedRoute?> PlanAsync(Location origin, Location destination, CancellationToken ct = default)
    {
        var apiKey = await _settings.GetTollGuruApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var payload = new
        {
            from = new { lat = origin.Latitude, lng = origin.Longitude },
            to = new { lat = destination.Latitude, lng = destination.Longitude },
            vehicleType = "2AxlesAuto",
            serviceProvider = "here"
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("x-api-key", apiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return Parse(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static PlannedRoute? Parse(JsonElement root)
    {
        // A resposta traz um objeto "route" (ou o primeiro de "routes").
        if (!TryGetRoute(root, out var route)) return null;

        var tolls = new List<PlannedToll>();
        if (route.TryGetProperty("tolls", out var tollArray) && tollArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tollArray.EnumerateArray())
            {
                var lat = GetDouble(t, "lat") ?? GetDouble(t, "latitude");
                var lng = GetDouble(t, "lng") ?? GetDouble(t, "longitude");
                if (lat is null || lng is null) continue;

                var name = GetString(t, "name") ?? GetString(t, "road") ?? "Pedágio";
                var price = GetDecimal(t, "cashCost") ?? GetDecimal(t, "tagCost") ?? GetDecimal(t, "cost") ?? 0m;
                tolls.Add(new PlannedToll(name, lat.Value, lng.Value, price));
            }
        }

        var path = new List<Location>();
        var polyline = GetString(route, "polyline");
        if (!string.IsNullOrEmpty(polyline))
            path = DecodePolyline(polyline!);

        return new PlannedRoute(path, tolls);
    }

    private static bool TryGetRoute(JsonElement root, out JsonElement route)
    {
        if (root.TryGetProperty("route", out route) && route.ValueKind == JsonValueKind.Object)
            return true;
        if (root.TryGetProperty("routes", out var routes) && routes.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in routes.EnumerateArray()) { route = r; return true; }
        }
        route = default;
        return false;
    }

    private static string? GetString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? GetDouble(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static decimal? GetDecimal(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    /// <summary>Decodifica uma polyline codificada do Google em uma lista de coordenadas.</summary>
    private static List<Location> DecodePolyline(string encoded)
    {
        var poly = new List<Location>();
        int index = 0, lat = 0, lng = 0;

        while (index < encoded.Length)
        {
            lat += DecodeDelta(encoded, ref index);
            lng += DecodeDelta(encoded, ref index);
            poly.Add(new Location(lat / 1e5, lng / 1e5));
        }
        return poly;
    }

    private static int DecodeDelta(string encoded, ref int index)
    {
        int result = 0, shift = 0, b;
        do
        {
            b = encoded[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
        } while (b >= 0x20 && index < encoded.Length);
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
