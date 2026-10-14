using System.Text;
using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using Haversine = FinanceApp.Application.Trips.TripService;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Implementação de <see cref="IRoutingService"/> 100% gratuita e sem cadastro, usando OpenStreetMap:
/// OSRM (servidor público de demonstração) para o traçado da rota e Overpass para as praças de pedágio
/// no caminho. Como o OSM não traz preço, o valor vem de uma tabela ANTT embutida
/// (Resources/Raw/pedagios_antt.json), casada por proximidade; sem correspondência, fica 0 para o
/// usuário editar.
/// </summary>
public sealed class OsmRoutingService : IRoutingService
{
    private const string OsrmUrl = "https://router.project-osrm.org/route/v1/driving/";
    private const string OverpassUrl = "https://overpass-api.de/api/interpreter";
    private const double TollNearRouteKm = 1.0;   // praça precisa estar a até 1 km do traçado
    private const double PriceMatchKm = 2.5;       // casa com a tarifa da tabela a até 2,5 km

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private IReadOnlyList<TollPrice>? _priceTable;

    private sealed record TollPrice(string Name, double Lat, double Lng, decimal Price);

    public async Task<PlannedRoute?> PlanAsync(Location origin, Location destination, CancellationToken ct = default)
    {
        var path = await GetRouteAsync(origin, destination, ct);
        if (path.Count < 2) return null;

        var tolls = await GetTollsAsync(path, ct);
        return new PlannedRoute(path, tolls);
    }

    // ---- rota (OSRM) ----

    private async Task<List<Location>> GetRouteAsync(Location origin, Location destination, CancellationToken ct)
    {
        try
        {
            var url = $"{OsrmUrl}{Inv(origin.Longitude)},{Inv(origin.Latitude)};{Inv(destination.Longitude)},{Inv(destination.Latitude)}?overview=full&geometries=polyline";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return new();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
                return new();

            var geometry = routes[0].TryGetProperty("geometry", out var g) ? g.GetString() : null;
            return string.IsNullOrEmpty(geometry) ? new() : DecodePolyline(geometry!);
        }
        catch { return new(); }
    }

    // ---- pedágios (Overpass + tabela ANTT) ----

    private async Task<List<PlannedToll>> GetTollsAsync(List<Location> path, CancellationToken ct)
    {
        var result = new List<PlannedToll>();
        try
        {
            var (south, west, north, east) = BoundingBox(path);
            var query = $"[out:json][timeout:25];node[\"barrier\"=\"toll_booth\"]({Inv(south)},{Inv(west)},{Inv(north)},{Inv(east)});out;";

            using var content = new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded");
            using var response = await _http.PostAsync(OverpassUrl, content, ct);
            if (!response.IsSuccessStatusCode) return result;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("elements", out var elements)) return result;

            var prices = await GetPriceTableAsync();

            foreach (var el in elements.EnumerateArray())
            {
                if (!el.TryGetProperty("lat", out var latEl) || !el.TryGetProperty("lon", out var lngEl)) continue;
                double lat = latEl.GetDouble(), lng = lngEl.GetDouble();

                // Mantém só as praças que ficam realmente sobre o traçado.
                if (MinDistanceToPathKm(path, lat, lng) > TollNearRouteKm) continue;

                var osmName = el.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var n)
                    ? n.GetString()
                    : null;

                var (price, tableName) = LookupPrice(prices, lat, lng);
                result.Add(new PlannedToll(osmName ?? tableName ?? "Pedágio", lat, lng, price));
            }
        }
        catch { /* Overpass pode falhar/limitar; devolve o que tiver */ }
        return result;
    }

    private static (decimal Price, string? Name) LookupPrice(IReadOnlyList<TollPrice> prices, double lat, double lng)
    {
        TollPrice? best = null;
        double bestKm = double.MaxValue;
        foreach (var p in prices)
        {
            var km = Haversine.HaversineKm(lat, lng, p.Lat, p.Lng);
            if (km < bestKm) { bestKm = km; best = p; }
        }
        return best is not null && bestKm <= PriceMatchKm ? (best.Price, best.Name) : (0m, null);
    }

    private async Task<IReadOnlyList<TollPrice>> GetPriceTableAsync()
    {
        if (_priceTable is not null) return _priceTable;
        var list = new List<TollPrice>();
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("pedagios_antt.json");
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("pedagios", out var arr))
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var name = e.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!e.TryGetProperty("lat", out var la) || !e.TryGetProperty("lng", out var lo) || !e.TryGetProperty("price", out var pr))
                        continue;
                    list.Add(new TollPrice(name ?? "Pedágio", la.GetDouble(), lo.GetDouble(), pr.GetDecimal()));
                }
            }
        }
        catch { /* sem tabela: todos os pedágios ficam com valor 0 para editar */ }
        _priceTable = list;
        return list;
    }

    // ---- geometria ----

    private static (double South, double West, double North, double East) BoundingBox(List<Location> path)
    {
        double south = double.MaxValue, west = double.MaxValue, north = double.MinValue, east = double.MinValue;
        foreach (var p in path)
        {
            south = Math.Min(south, p.Latitude);
            north = Math.Max(north, p.Latitude);
            west = Math.Min(west, p.Longitude);
            east = Math.Max(east, p.Longitude);
        }
        return (south, west, north, east);
    }

    private static double MinDistanceToPathKm(List<Location> path, double lat, double lng)
    {
        double min = double.MaxValue;
        foreach (var p in path)
        {
            var km = Haversine.HaversineKm(lat, lng, p.Latitude, p.Longitude);
            if (km < min) min = km;
        }
        return min;
    }

    private static string Inv(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Decodifica uma polyline codificada (precisão 5) em coordenadas.</summary>
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
