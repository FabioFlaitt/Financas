using FinanceApp.Application.Trips;
using FinanceApp.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class TripServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static TripService Build(DateOnly today = default)
    {
        var db = TestDb.NewContext();
        var clock = new FakeClock(today == default ? new DateOnly(2026, 6, 1) : today);
        return new TripService(db, new FakeCurrentUser(UserId), clock);
    }

    [Fact]
    public void Haversine_computes_known_distance()
    {
        // São Paulo (Sé) -> Campinas ~ 90 km em linha reta
        var km = TripService.HaversineKm(-23.5505, -46.6333, -22.9099, -47.0626);
        km.Should().BeInRange(80, 100);
    }

    [Fact]
    public async Task Accumulates_distance_from_location_points()
    {
        var svc = Build();
        var trip = await svc.StartAsync(new StartTripRequest { Name = "Viagem" });

        await svc.AddLocationPointAsync(trip.Id, new AddLocationPointRequest { Latitude = -23.5505, Longitude = -46.6333 });
        var summary = await svc.AddLocationPointAsync(trip.Id, new AddLocationPointRequest { Latitude = -22.9099, Longitude = -47.0626 });

        summary.DistanceKm.Should().BeInRange(80, 100);
    }

    [Fact]
    public async Task Summary_computes_totals_and_cost_per_km()
    {
        var svc = Build();
        var trip = await svc.StartAsync(new StartTripRequest
        {
            Name = "SP -> Rio",
            StartOdometerKm = 1000m
        });

        await svc.AddExpenseAsync(trip.Id, new AddTripExpenseRequest { Type = TripExpenseType.Toll, Amount = 30m });
        await svc.AddExpenseAsync(trip.Id, new AddTripExpenseRequest { Type = TripExpenseType.Toll, Amount = 20m });
        await svc.AddExpenseAsync(trip.Id, new AddTripExpenseRequest { Type = TripExpenseType.Fuel, Amount = 250m, Liters = 40m });

        var finished = await svc.FinishAsync(trip.Id, new FinishTripRequest { EndOdometerKm = 1400m });

        finished.Summary.DistanceKm.Should().Be(400m);
        finished.Summary.TollCount.Should().Be(2);
        finished.Summary.TollTotal.Should().Be(50m);
        finished.Summary.FuelTotal.Should().Be(250m);
        finished.Summary.TotalCost.Should().Be(300m);
        finished.Summary.CostPerKm.Should().Be(0.75m);      // 300 / 400
        finished.Summary.ActualKmPerLiter.Should().Be(10m);  // 400 / 40
    }

    [Fact]
    public async Task Finish_sets_status_and_time()
    {
        var svc = Build();
        var trip = await svc.StartAsync(new StartTripRequest { Name = "Teste" });

        var finished = await svc.FinishAsync(trip.Id, new FinishTripRequest());

        finished.Status.Should().Be(TripStatus.Finished);
        finished.FinishedAtUtc.Should().NotBeNull();
    }
}
