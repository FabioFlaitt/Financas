using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Busca de lugares via Photon (photon.komoot.io), baseado em OpenStreetMap — grátis e sem chave.
/// Falhas de rede devolvem lista vazia/null; a tela segue funcionando (dá pra tocar no mapa).
/// </summary>
public sealed class PhotonPlaceSearchService : IPlaceSearchService
{
    private const string SearchUrl = "https://photon.komoot.io/api/";
    private const string ReverseUrl = "https://photon.komoot.io/reverse/";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<IReadOnlyList<PlaceResult>> SearchAsync(string query, Location? near = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3) return Array.Empty<PlaceResult>();

        // Photon só aceita lang de/en/fr/it (pt dá 400); sem lang ele devolve os nomes do próprio OSM.
        var url = new StringBuilder(SearchUrl)
            .Append("?q=").Append(Uri.EscapeDataString(query.Trim()))
            .Append("&limit=6");
        if (near is not null)
            url.Append("&lat=").Append(Inv(near.Latitude)).Append("&lon=").Append(Inv(near.Longitude));

        try
        {
            using var response = await _http.GetAsync(url.ToString(), ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<PlaceResult>();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseFeatures(doc.RootElement);
        }
        catch { return Array.Empty<PlaceResult>(); }
    }

    public async Task<string?> ReverseAsync(Location location, CancellationToken ct = default)
    {
        try
        {
            var url = $"{ReverseUrl}?lat={Inv(location.Latitude)}&lon={Inv(location.Longitude)}";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseFeatures(doc.RootElement).FirstOrDefault()?.Label;
        }
        catch { return null; }
    }

    private static List<PlaceResult> ParseFeatures(JsonElement root)
    {
        var results = new List<PlaceResult>();
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var f in features.EnumerateArray())
        {
            if (!f.TryGetProperty("geometry", out var geom) ||
                !geom.TryGetProperty("coordinates", out var coords) || coords.GetArrayLength() < 2)
                continue;

            double lng = coords[0].GetDouble(), lat = coords[1].GetDouble();
            var label = BuildLabel(f.TryGetProperty("properties", out var p) ? p : default);
            if (!string.IsNullOrWhiteSpace(label))
                results.Add(new PlaceResult(label, lat, lng));
        }
        return results;
    }

    private static string BuildLabel(JsonElement props)
    {
        if (props.ValueKind != JsonValueKind.Object) return string.Empty;

        string? Get(string name) => props.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var parts = new[] { Get("name"), Get("street"), Get("city") ?? Get("county"), Get("state") }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct();
        return string.Join(", ", parts);
    }

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
