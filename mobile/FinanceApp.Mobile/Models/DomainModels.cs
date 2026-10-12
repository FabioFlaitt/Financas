namespace FinanceApp.Mobile.Models;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColorHex { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public Guid? CreditCardId { get; set; }
    public string? Merchant { get; set; }
    public string? Notes { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public class AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public AccountType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string ColorHex { get; set; } = "#1565C0";
    public bool IncludeInTotalBalance { get; set; }
    public bool IsArchived { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#607D8B";
    public string Icon { get; set; } = "tag";
    public ExpenseNature DefaultNature { get; set; }
    public string NatureLabel { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

public class RecurringTransactionDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public RecurrenceFrequency Frequency { get; set; }
    public string FrequencyLabel { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? NextOccurrence { get; set; }
    public bool IsActive { get; set; }
}

// ---- Viagens ----

public class TripSummary
{
    public decimal DistanceKm { get; set; }
    public int TollCount { get; set; }
    public decimal TollTotal { get; set; }
    public decimal FuelTotal { get; set; }
    public decimal FuelLiters { get; set; }
    public decimal OtherTotal { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostPerKm { get; set; }
    public decimal? ActualKmPerLiter { get; set; }
    public double DurationHours { get; set; }
}

public class TripDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? VehicleName { get; set; }
    public TripStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public TripSummary Summary { get; set; } = new();

    public bool IsInProgress => Status == TripStatus.InProgress;
}

public class TripExpenseDto
{
    public Guid Id { get; set; }
    public TripExpenseType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? Liters { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class TripLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public double? SpeedKmh { get; set; }
}

public class TripDetailDto : TripDto
{
    public decimal? StartOdometerKm { get; set; }
    public decimal? EndOdometerKm { get; set; }
    public decimal? FuelPricePerLiter { get; set; }
    public decimal? FuelEfficiencyKmPerLiter { get; set; }
    public string? Notes { get; set; }
    public List<TripExpenseDto> Expenses { get; set; } = new();
    public List<TripLocationDto> Route { get; set; } = new();
}
