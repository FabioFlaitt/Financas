using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>Um pedágio previsto na rota: onde fica e quanto custa.</summary>
public record PlannedToll(string Name, double Latitude, double Longitude, decimal Price);

/// <summary>Uma manobra da rota (ex.: "Vire à direita") e onde ela acontece, para falar na hora certa.</summary>
public record NavStep(double Latitude, double Longitude, string Instruction);

/// <summary>Resultado do planejamento: o traçado da rota, pedágios, manobras e totais (distância/tempo).</summary>
public record PlannedRoute(
    IReadOnlyList<Location> Path,
    IReadOnlyList<PlannedToll> Tolls,
    IReadOnlyList<NavStep> Steps,
    double DistanceKm,
    double DurationMinutes);

/// <summary>
/// Calcula a rota entre dois pontos e os pedágios no caminho (com preço). Isolado atrás da interface
/// para que o resto do app não dependa do provedor (hoje OpenStreetMap: OSRM + Overpass, grátis e sem chave).
/// </summary>
public interface IRoutingService
{
    /// <summary>Planeja a rota. Devolve null se os provedores não responderem.</summary>
    Task<PlannedRoute?> PlanAsync(Location origin, Location destination, CancellationToken ct = default);
}
