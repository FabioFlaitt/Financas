using FinanceApp.Application.Accounts;
using FinanceApp.Application.Categories;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceApp.Tests;

/// <summary>Valida o modo local (app no celular): mesmos serviços, sobre um arquivo SQLite, sem servidor.</summary>
public class LocalModeTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"financas-{Guid.NewGuid():N}.db3");

    private async Task<ServiceProvider> BuildAsync()
    {
        var provider = new ServiceCollection().AddLocalFinance(_path).BuildServiceProvider();
        await provider.InitializeLocalAsync();
        return provider;
    }

    [Fact]
    public async Task First_run_creates_profile_with_default_categories_and_preferences()
    {
        await using var provider = await BuildAsync();
        using var scope = provider.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<ICategoryService>().ListAsync()).Should().NotBeEmpty();
        (await scope.ServiceProvider.GetRequiredService<INotificationPreferenceService>().ListAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Initialization_is_idempotent_and_data_survives_restart()
    {
        await using (var first = await BuildAsync())
        {
            using var scope = first.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAccountService>()
                .CreateAsync(new SaveAccountRequest { Name = "Carteira", InitialBalance = 100m });
        }

        // "Reabre o app": novo provedor sobre o mesmo arquivo.
        await using var second = await BuildAsync();
        using var scope2 = second.CreateScope();
        var accounts = await scope2.ServiceProvider.GetRequiredService<IAccountService>().ListAsync();
        accounts.Should().ContainSingle(a => a.Name == "Carteira");

        var categories = await scope2.ServiceProvider.GetRequiredService<ICategoryService>().ListAsync();
        categories.Select(c => c.Name).Should().OnlyHaveUniqueItems(); // perfil não foi duplicado
    }

    [Fact]
    public async Task Transaction_flows_into_dashboard()
    {
        await using var provider = await BuildAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var account = await sp.GetRequiredService<IAccountService>()
            .CreateAsync(new SaveAccountRequest { Name = "Corrente", InitialBalance = 1000m });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await sp.GetRequiredService<ITransactionService>().CreateAsync(new CreateTransactionRequest
        {
            Description = "Salário", Amount = 3000m, Type = TransactionType.Income,
            TransactionDate = today, AccountId = account.Id
        });
        await sp.GetRequiredService<ITransactionService>().CreateAsync(new CreateTransactionRequest
        {
            Description = "Mercado", Amount = 250.50m, Type = TransactionType.Expense,
            TransactionDate = today, AccountId = account.Id
        });

        var dash = await sp.GetRequiredService<IDashboardService>()
            .GetAsync(new DashboardQuery { Year = today.Year, Month = today.Month });

        dash.MonthIncome.Should().Be(3000m);
        dash.MonthExpense.Should().Be(250.50m);
        dash.TotalBalance.Should().Be(3749.50m);
    }

    [Fact]
    public async Task Backup_via_vacuum_into_produces_a_readable_copy()
    {
        await using var provider = await BuildAsync();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>()
                .CreateAsync(new SaveAccountRequest { Name = "Backup" });
            var db = scope.ServiceProvider.GetRequiredService<FinanceApp.Infrastructure.Persistence.AppDbContext>();
            var target = _path + ".bak";
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(db.Database, $"VACUUM INTO {target}");
            File.Exists(target).Should().BeTrue();
            new FileInfo(target).Length.Should().BeGreaterThan(0);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(target);
        }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            try { File.Delete(f); } catch { }
    }
}
