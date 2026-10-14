using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Trips;

public interface ITripService
{
    Task<IReadOnlyList<TripDto>> ListAsync(CancellationToken ct = default);
    Task<TripDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<TripDetailDto> StartAsync(StartTripRequest request, CancellationToken ct = default);
    Task<TripDetailDto> UpdateAsync(Guid id, UpdateTripRequest request, CancellationToken ct = default);
    Task<TripDetailDto> FinishAsync(Guid id, FinishTripRequest request, CancellationToken ct = default);
    Task<TripDetailDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TripDto>> ListDeletedAsync(CancellationToken ct = default);

    Task<TripExpenseDto> AddExpenseAsync(Guid tripId, AddTripExpenseRequest request, CancellationToken ct = default);
    Task<TripExpenseDto> UpdateExpenseAsync(Guid tripId, Guid expenseId, UpdateTripExpenseRequest request, CancellationToken ct = default);
    Task DeleteExpenseAsync(Guid tripId, Guid expenseId, CancellationToken ct = default);

    Task<TripSummary> AddLocationPointAsync(Guid tripId, AddLocationPointRequest request, CancellationToken ct = default);
}

public class TripService : ITripService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public TripService(IApplicationDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<TripDto>> ListAsync(CancellationToken ct = default)
        => await ListInternalAsync(deleted: false, ct);

    public async Task<IReadOnlyList<TripDto>> ListDeletedAsync(CancellationToken ct = default)
        => await ListInternalAsync(deleted: true, ct);

    private async Task<IReadOnlyList<TripDto>> ListInternalAsync(bool deleted, CancellationToken ct)
    {
        var trips = await _db.Trips.Where(t => t.UserId == UserId && t.IsDeleted == deleted).ToListAsync(ct);
        var expenses = await _db.TripExpenses.Where(e => e.UserId == UserId).ToListAsync(ct);
        var byTrip = expenses.GroupBy(e => e.TripId).ToDictionary(g => g.Key, g => g.ToList());

        return trips
            .OrderByDescending(t => t.StartedAtUtc)
            .Select(t => ToDto(t, byTrip.GetValueOrDefault(t.Id) ?? new()))
            .ToList();
    }

    public async Task<TripDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        var expenses = await _db.TripExpenses.Where(e => e.TripId == id).OrderBy(e => e.OccurredAtUtc).ToListAsync(ct);
        var route = await _db.TripLocationPoints.Where(p => p.TripId == id).OrderBy(p => p.RecordedAtUtc).ToListAsync(ct);
        return ToDetail(trip, expenses, route);
    }

    public async Task<TripDetailDto> StartAsync(StartTripRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException(nameof(request.Name), "Dê um nome à viagem.");

        var trip = new Trip
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            Origin = request.Origin?.Trim(),
            Destination = request.Destination?.Trim(),
            VehicleName = request.VehicleName?.Trim(),
            OriginLatitude = request.OriginLatitude,
            OriginLongitude = request.OriginLongitude,
            DestinationLatitude = request.DestinationLatitude,
            DestinationLongitude = request.DestinationLongitude,
            Status = TripStatus.InProgress,
            StartedAtUtc = _clock.UtcNow,
            StartOdometerKm = request.StartOdometerKm,
            FuelPricePerLiter = request.FuelPricePerLiter,
            FuelEfficiencyKmPerLiter = request.FuelEfficiencyKmPerLiter,
            Notes = request.Notes
        };
        _db.Trips.Add(trip);
        await _db.SaveChangesAsync(ct);
        return ToDetail(trip, new(), new());
    }

    public async Task<TripDetailDto> UpdateAsync(Guid id, UpdateTripRequest request, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException(nameof(request.Name), "Dê um nome à viagem.");

        trip.Name = request.Name.Trim();
        trip.Origin = request.Origin?.Trim();
        trip.Destination = request.Destination?.Trim();
        trip.VehicleName = request.VehicleName?.Trim();
        trip.StartOdometerKm = request.StartOdometerKm;
        trip.EndOdometerKm = request.EndOdometerKm;
        trip.FuelPricePerLiter = request.FuelPricePerLiter;
        trip.FuelEfficiencyKmPerLiter = request.FuelEfficiencyKmPerLiter;
        trip.Notes = request.Notes;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDetailDto> FinishAsync(Guid id, FinishTripRequest request, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        trip.Status = TripStatus.Finished;
        trip.FinishedAtUtc = _clock.UtcNow;
        if (request.EndOdometerKm is decimal end) trip.EndOdometerKm = end;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDetailDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        trip.Status = TripStatus.Canceled;
        trip.FinishedAtUtc = _clock.UtcNow;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        _db.Trips.Remove(trip);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        trip.IsDeleted = true;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await LoadAsync(id, ct);
        trip.IsDeleted = false;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TripExpenseDto> AddExpenseAsync(Guid tripId, AddTripExpenseRequest request, CancellationToken ct = default)
    {
        var trip = await LoadAsync(tripId, ct);
        if (request.Amount <= 0)
            throw new AppValidationException(nameof(request.Amount), "O valor deve ser maior que zero.");

        var expense = new TripExpense
        {
            UserId = UserId,
            TripId = trip.Id,
            Type = request.Type,
            Description = request.Description?.Trim(),
            Amount = Math.Abs(request.Amount),
            Liters = request.Liters,
            OccurredAtUtc = _clock.UtcNow,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };
        _db.TripExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return ToExpenseDto(expense);
    }

    public async Task<TripExpenseDto> UpdateExpenseAsync(Guid tripId, Guid expenseId, UpdateTripExpenseRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new AppValidationException(nameof(request.Amount), "O valor deve ser maior que zero.");

        var expense = await _db.TripExpenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.TripId == tripId && e.UserId == UserId, ct)
            ?? throw NotFoundException.For("Gasto da viagem", expenseId);

        expense.Type = request.Type;
        expense.Description = request.Description?.Trim();
        expense.Amount = Math.Abs(request.Amount);
        expense.Liters = request.Liters;
        expense.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToExpenseDto(expense);
    }

    public async Task DeleteExpenseAsync(Guid tripId, Guid expenseId, CancellationToken ct = default)
    {
        var expense = await _db.TripExpenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.TripId == tripId && e.UserId == UserId, ct)
            ?? throw NotFoundException.For("Gasto da viagem", expenseId);
        _db.TripExpenses.Remove(expense);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TripSummary> AddLocationPointAsync(Guid tripId, AddLocationPointRequest request, CancellationToken ct = default)
    {
        var trip = await LoadAsync(tripId, ct);

        var last = await _db.TripLocationPoints
            .Where(p => p.TripId == tripId)
            .OrderByDescending(p => p.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (last is not null)
        {
            var segment = HaversineKm(last.Latitude, last.Longitude, request.Latitude, request.Longitude);
            // Ignora ruído de GPS parado (< 5 metros).
            if (segment >= 0.005)
                trip.GpsDistanceKm += (decimal)Math.Round(segment, 4);
        }

        _db.TripLocationPoints.Add(new TripLocationPoint
        {
            TripId = tripId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RecordedAtUtc = request.RecordedAtUtc ?? _clock.UtcNow,
            SpeedKmh = request.SpeedKmh
        });
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var expenses = await _db.TripExpenses.Where(e => e.TripId == tripId).ToListAsync(ct);
        return BuildSummary(trip, expenses);
    }

    // ---- helpers ----

    private async Task<Trip> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId, ct)
        ?? throw NotFoundException.For("Viagem", id);

    /// <summary>Distância entre dois pontos geográficos em km (fórmula de Haversine).</summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        double dLat = DegToRad(lat2 - lat1);
        double dLon = DegToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(DegToRad(lat1)) * Math.Cos(DegToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    private TripSummary BuildSummary(Trip trip, List<TripExpense> expenses)
    {
        decimal distance = trip.StartOdometerKm is decimal s && trip.EndOdometerKm is decimal e && e >= s
            ? e - s
            : trip.GpsDistanceKm;

        var tolls = expenses.Where(x => x.Type == TripExpenseType.Toll).ToList();
        var fuels = expenses.Where(x => x.Type == TripExpenseType.Fuel).ToList();
        var fuelTotal = fuels.Sum(x => x.Amount);
        var fuelLiters = fuels.Sum(x => x.Liters ?? 0m);
        var otherTotal = expenses.Where(x => x.Type is not TripExpenseType.Toll and not TripExpenseType.Fuel).Sum(x => x.Amount);
        var total = expenses.Sum(x => x.Amount);

        var end = trip.FinishedAtUtc ?? _clock.UtcNow;
        var duration = (end - trip.StartedAtUtc).TotalHours;

        return new TripSummary
        {
            DistanceKm = Math.Round(distance, 2),
            TollCount = tolls.Count,
            TollTotal = Math.Round(tolls.Sum(x => x.Amount), 2),
            FuelTotal = Math.Round(fuelTotal, 2),
            FuelLiters = Math.Round(fuelLiters, 2),
            OtherTotal = Math.Round(otherTotal, 2),
            TotalCost = Math.Round(total, 2),
            CostPerKm = distance > 0 ? Math.Round(total / distance, 2) : 0m,
            ActualKmPerLiter = fuelLiters > 0 && distance > 0 ? Math.Round(distance / fuelLiters, 2) : null,
            DurationHours = Math.Round(duration, 2)
        };
    }

    private TripDto ToDto(Trip trip, List<TripExpense> expenses) => new()
    {
        Id = trip.Id,
        Name = trip.Name,
        Origin = trip.Origin,
        Destination = trip.Destination,
        VehicleName = trip.VehicleName,
        Status = trip.Status,
        StatusLabel = trip.Status.GetDescription(),
        StartedAtUtc = trip.StartedAtUtc,
        FinishedAtUtc = trip.FinishedAtUtc,
        Summary = BuildSummary(trip, expenses)
    };

    private TripDetailDto ToDetail(Trip trip, List<TripExpense> expenses, List<TripLocationPoint> route) => new()
    {
        Id = trip.Id,
        Name = trip.Name,
        Origin = trip.Origin,
        Destination = trip.Destination,
        VehicleName = trip.VehicleName,
        Status = trip.Status,
        StatusLabel = trip.Status.GetDescription(),
        StartedAtUtc = trip.StartedAtUtc,
        FinishedAtUtc = trip.FinishedAtUtc,
        Summary = BuildSummary(trip, expenses),
        StartOdometerKm = trip.StartOdometerKm,
        EndOdometerKm = trip.EndOdometerKm,
        FuelPricePerLiter = trip.FuelPricePerLiter,
        FuelEfficiencyKmPerLiter = trip.FuelEfficiencyKmPerLiter,
        Notes = trip.Notes,
        OriginLatitude = trip.OriginLatitude,
        OriginLongitude = trip.OriginLongitude,
        DestinationLatitude = trip.DestinationLatitude,
        DestinationLongitude = trip.DestinationLongitude,
        Expenses = expenses.Select(ToExpenseDto).ToList(),
        Route = route.Select(p => new TripLocationDto(p.Latitude, p.Longitude, p.RecordedAtUtc, p.SpeedKmh)).ToList()
    };

    private static TripExpenseDto ToExpenseDto(TripExpense e) => new()
    {
        Id = e.Id,
        Type = e.Type,
        TypeLabel = e.Type.GetDescription(),
        Description = e.Description,
        Amount = e.Amount,
        Liters = e.Liters,
        OccurredAtUtc = e.OccurredAtUtc,
        Latitude = e.Latitude,
        Longitude = e.Longitude
    };
}
