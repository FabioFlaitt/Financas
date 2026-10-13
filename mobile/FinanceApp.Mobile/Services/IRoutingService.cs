using Microsoft.Maui.Devices.Sensors;

namespace FinanceApp.Mobile.Services;

/// <summary>Um pedágio previsto na rota: onde fica e quanto custa.</summary>
public record PlannedToll(string Name, double Latitude, double Longitude, decimal Price);

/// <summary>Resultado do planejamento: o traçado da rota (para desenhar) e os pedágios previstos.</summary>
public record PlannedRoute(IReadOnlyList<Location> Path, IReadOnlyList<PlannedToll> Tolls);

/// <summary>
/// Calcula a rota entre dois pontos e os pedágios no caminho (com preço). Isolado atrás da interface
/// para que o resto do app não dependa do provedor (hoje TollGuru).
/// </summary>
public interface IRoutingService
{
    /// <summary>true quando há chave de API configurada — sem ela o planejamento não funciona.</summary>
    Task<bool> IsConfiguredAsync();

    /// <summary>Planeja a rota. Devolve null se não houver chave ou o provedor não responder.</summary>
    Task<PlannedRoute?> PlanAsync(Location origin, Location destination, CancellationToken ct = default);
}
