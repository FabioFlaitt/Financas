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
        // FINANCE_TEST_SQLITE=1 roda a suíte sobre SQLite (o mesmo provedor do modo local do app).
        if (Environment.GetEnvironmentVariable("FINANCE_TEST_SQLITE") == "1")
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            // Os testes inserem dados sem criar o usuário dono (o InMemory tolera); aqui interessa validar as consultas.
            using (var pragma = connection.CreateCommand()) { pragma.CommandText = "PRAGMA foreign_keys=OFF"; pragma.ExecuteNonQuery(); }
            var sqlite = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            sqlite.Database.EnsureCreated();
            return sqlite;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
