using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Viagem de carro. Acompanha distância percorrida (por GPS e/ou odômetro), pedágios,
/// abastecimentos e demais gastos, permitindo calcular o custo total e o custo por km.
/// </summary>
public class Trip : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? VehicleName { get; set; }

    /// <summary>Coordenadas de origem/destino escolhidas no planejamento (para re-planejar sem geocodificar de novo).</summary>
    public double? OriginLatitude { get; set; }
    public double? OriginLongitude { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }

    /// <summary>Exclusão lógica: a viagem some da lista mas continua no banco (recuperável).</summary>
    public bool IsDeleted { get; set; }

    public TripStatus Status { get; set; } = TripStatus.InProgress;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Distância acumulada por GPS (soma de trechos entre pontos), em km.</summary>
    public decimal GpsDistanceKm { get; set; }

    /// <summary>Odômetro inicial/final (km), quando o usuário preferir informar manualmente.</summary>
    public decimal? StartOdometerKm { get; set; }
    public decimal? EndOdometerKm { get; set; }

    /// <summary>Preço de referência do combustível por litro (opcional, para estimativas).</summary>
    public decimal? FuelPricePerLiter { get; set; }

    /// <summary>Consumo médio do veículo em km/l (opcional, para estimativas).</summary>
    public decimal? FuelEfficiencyKmPerLiter { get; set; }

    public string? Notes { get; set; }

    public ICollection<TripExpense> Expenses { get; set; } = new List<TripExpense>();
    public ICollection<TripLocationPoint> LocationPoints { get; set; } = new List<TripLocationPoint>();
}

/// <summary>Gasto registrado durante uma viagem (pedágio, combustível, etc.).</summary>
public class TripExpense : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public TripExpenseType Type { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Litros abastecidos (apenas para combustível).</summary>
    public decimal? Liters { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>Ponto de localização capturado durante a viagem (para traçar a rota e somar os km).</summary>
public class TripLocationPoint : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Velocidade instantânea em km/h, quando disponível.</summary>
    public double? SpeedKmh { get; set; }
}
