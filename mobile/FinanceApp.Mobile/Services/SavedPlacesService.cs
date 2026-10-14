using System.Text.Json;
using Microsoft.Maui.Storage;

namespace FinanceApp.Mobile.Services;

/// <summary>Um destino salvo (recente ou favorito).</summary>
public class SavedPlace
{
    public string Label { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime LastUsedUtc { get; set; }
}

/// <summary>
/// Guarda os destinos recentes e favoritos num arquivo JSON local (AppDataDirectory). Entra no backup
/// do aparelho e não depende de servidor. Mantém os últimos recentes e todos os favoritos.
/// </summary>
public class SavedPlacesService
{
    private const int MaxRecents = 8;
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "saved_places.json");
    private List<SavedPlace>? _cache;

    public async Task<IReadOnlyList<SavedPlace>> GetAllAsync()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(_path))
            {
                await using var stream = File.OpenRead(_path);
                _cache = await JsonSerializer.DeserializeAsync<List<SavedPlace>>(stream) ?? new();
            }
            else _cache = new();
        }
        catch { _cache = new(); }
        return _cache;
    }

    /// <summary>Favoritos primeiro, depois os recentes mais novos.</summary>
    public async Task<IReadOnlyList<SavedPlace>> GetSuggestionsAsync()
    {
        var all = await GetAllAsync();
        return all.OrderByDescending(p => p.IsFavorite).ThenByDescending(p => p.LastUsedUtc).ToList();
    }

    public async Task AddRecentAsync(PlaceResult place)
    {
        var all = (await GetAllAsync()).ToList();
        var existing = all.FirstOrDefault(p => Close(p, place));
        if (existing is not null)
        {
            existing.LastUsedUtc = DateTime.UtcNow;
            existing.Label = place.Label;
        }
        else
        {
            all.Add(new SavedPlace
            {
                Label = place.Label,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                LastUsedUtc = DateTime.UtcNow
            });
        }

        // Mantém todos os favoritos + os recentes mais novos (limite).
        var trimmed = all.Where(p => p.IsFavorite)
            .Concat(all.Where(p => !p.IsFavorite).OrderByDescending(p => p.LastUsedUtc).Take(MaxRecents))
            .ToList();
        await SaveAsync(trimmed);
    }

    public async Task ToggleFavoriteAsync(SavedPlace place)
    {
        var all = (await GetAllAsync()).ToList();
        var match = all.FirstOrDefault(p => Close(p, new PlaceResult(place.Label, place.Latitude, place.Longitude)));
        if (match is not null) { match.IsFavorite = !match.IsFavorite; await SaveAsync(all); }
    }

    private async Task SaveAsync(List<SavedPlace> places)
    {
        _cache = places;
        try
        {
            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, places);
        }
        catch { /* se não der pra salvar, segue com o cache em memória */ }
    }

    private static bool Close(SavedPlace a, PlaceResult b)
        => Math.Abs(a.Latitude - b.Latitude) < 0.0005 && Math.Abs(a.Longitude - b.Longitude) < 0.0005;
}
