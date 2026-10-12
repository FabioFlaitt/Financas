using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Dados de DEMONSTRAÇÃO (fictícios) para a aba de Viagens quando a API não está acessível,
/// para que a tela tenha conteúdo no primeiro uso. Sempre identificados como demonstração.
/// </summary>
public static class DemoTripsData
{
    public static List<TripDto> Build()
    {
        var now = DateTime.Now;

        return new List<TripDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "São Paulo → Campos do Jordão",
                Origin = "São Paulo, SP",
                Destination = "Campos do Jordão, SP",
                VehicleName = "Carro",
                Status = TripStatus.Finished,
                StatusLabel = "Concluída",
                StartedAtUtc = now.AddDays(-9).AddHours(-3),
                FinishedAtUtc = now.AddDays(-9),
                Summary = new TripSummary
                {
                    DistanceKm = 184m,
                    TollCount = 3,
                    TollTotal = 42.60m,
                    FuelTotal = 148.00m,
                    FuelLiters = 24.5m,
                    OtherTotal = 35.00m,
                    TotalCost = 225.60m,
                    CostPerKm = 1.23m,
                    ActualKmPerLiter = 7.5m,
                    DurationHours = 3.1
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Viagem a trabalho — Santos",
                Origin = "São Paulo, SP",
                Destination = "Santos, SP",
                VehicleName = "Carro",
                Status = TripStatus.Finished,
                StatusLabel = "Concluída",
                StartedAtUtc = now.AddDays(-3).AddHours(-2),
                FinishedAtUtc = now.AddDays(-3),
                Summary = new TripSummary
                {
                    DistanceKm = 78m,
                    TollCount = 2,
                    TollTotal = 28.80m,
                    FuelTotal = 62.00m,
                    FuelLiters = 10.2m,
                    OtherTotal = 0m,
                    TotalCost = 90.80m,
                    CostPerKm = 1.16m,
                    ActualKmPerLiter = 7.6m,
                    DurationHours = 1.4
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Volta ao interior (em andamento)",
                Origin = "São Paulo, SP",
                Destination = "Ribeirão Preto, SP",
                VehicleName = "Carro",
                Status = TripStatus.InProgress,
                StatusLabel = "Em andamento",
                StartedAtUtc = now.AddHours(-1),
                FinishedAtUtc = null,
                Summary = new TripSummary
                {
                    DistanceKm = 92m,
                    TollCount = 1,
                    TollTotal = 14.20m,
                    FuelTotal = 70.00m,
                    FuelLiters = 11.5m,
                    OtherTotal = 0m,
                    TotalCost = 84.20m,
                    CostPerKm = 0.92m,
                    ActualKmPerLiter = 8.0m,
                    DurationHours = 1.0
                }
            }
        };
    }
}
