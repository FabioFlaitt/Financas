using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests;

/// <summary>Relógio determinístico para testes.</summary>
public class FakeClock : IDateTimeProvider
{
    public FakeClock(DateOnly today) => Today = today;
    public DateOnly Today { get; }
    public DateTime UtcNow => Today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

/// <summary>Usuário atual fixo para testes.</summary>
public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(Guid userId) => UserId = userId;
    public Guid? UserId { get; set; }
    public bool IsAuthenticated => UserId is not null;
}

public static class TestDb
{
    /// <summary>Cria um <see cref="AppDbContext"/> isolado em memória para cada teste.</summary>
    public static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
