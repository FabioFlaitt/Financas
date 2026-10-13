using FinanceApp.Domain.Enums;

namespace FinanceApp.Application.Trips;

public record TripSummary
{
    public decimal DistanceKm { get; init; }
    public int TollCount { get; init; }
    public decimal TollTotal { get; init; }
    public decimal FuelTotal { get; init; }
    public decimal FuelLiters { get; init; }
    public decimal OtherTotal { get; init; }
    public decimal TotalCost { get; init; }
    public decimal CostPerKm { get; init; }
    /// <summary>Consumo real calculado (km/l), quando houver litros e distância.</summary>
    public decimal? ActualKmPerLiter { get; init; }
    public double DurationHours { get; init; }
}

public record TripDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Origin { get; init; }
    public string? Destination { get; init; }
    public string? VehicleName { get; init; }
    public TripStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? FinishedAtUtc { get; init; }
    public TripSummary Summary { get; init; } = new();
}

public record TripExpenseDto
{
    public Guid Id { get; init; }
    public TripExpenseType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public decimal? Liters { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public record TripLocationDto(double Latitude, double Longitude, DateTime RecordedAtUtc, double? SpeedKmh);

public record TripDetailDto : TripDto
{
    public decimal? StartOdometerKm { get; init; }
    public decimal? EndOdometerKm { get; init; }
    public decimal? FuelPricePerLiter { get; init; }
    public decimal? FuelEfficiencyKmPerLiter { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<TripExpenseDto> Expenses { get; init; } = Array.Empty<TripExpenseDto>();
    public IReadOnlyList<TripLocationDto> Route { get; init; } = Array.Empty<TripLocationDto>();
}

public record StartTripRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Origin { get; init; }
    public string? Destination { get; init; }
    public string? VehicleName { get; init; }
    public decimal? StartOdometerKm { get; init; }
    public decimal? FuelPricePerLiter { get; init; }
    public decimal? FuelEfficiencyKmPerLiter { get; init; }
    public string? Notes { get; init; }
}

public record UpdateTripRequest : StartTripRequest
{
    public decimal? EndOdometerKm { get; init; }
}

public record FinishTripRequest
{
    public decimal? EndOdometerKm { get; init; }
}

public record AddTripExpenseRequest
{
    public TripExpenseType Type { get; init; } = TripExpenseType.Toll;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public decimal? Liters { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public record UpdateTripExpenseRequest
{
    public TripExpenseType Type { get; init; } = TripExpenseType.Toll;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public decimal? Liters { get; init; }
}

public record AddLocationPointRequest
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public DateTime? RecordedAtUtc { get; init; }
    public double? SpeedKmh { get; init; }
}
