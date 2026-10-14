using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>Um lugar encontrado na busca: rótulo legível + coordenada.</summary>
public record PlaceResult(string Label, double Latitude, double Longitude);

/// <summary>
/// Busca de endereços/lugares (autocomplete) e geocodificação reversa. Isolado atrás da interface;
/// hoje usa o Photon (OpenStreetMap), grátis e sem chave.
/// </summary>
public interface IPlaceSearchService
{
    /// <summary>Sugestões para o texto digitado, opcionalmente enviesadas para perto de um ponto.</summary>
    Task<IReadOnlyList<PlaceResult>> SearchAsync(string query, Location? near = null, CancellationToken ct = default);

    /// <summary>Converte uma coordenada em um rótulo legível (ex.: para mostrar "Seu local").</summary>
    Task<string?> ReverseAsync(Location location, CancellationToken ct = default);
}
