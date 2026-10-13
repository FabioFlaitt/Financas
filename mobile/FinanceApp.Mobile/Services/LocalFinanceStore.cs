using FinanceApp.Infrastructure;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Banco de dados LOCAL do app: um arquivo SQLite na área privada do aplicativo (sandbox), sem servidor
/// e sem rede. Roda as mesmas regras de negócio do backend (orçamento, faturas, importação, relatórios…).
/// Os dados nunca saem do aparelho, a menos que o usuário exporte um backup.
/// </summary>
public sealed class LocalFinanceStore
{
    public const string FileName = "financas.db3";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceProvider? _provider;

    public string DatabasePath { get; } = Path.Combine(FileSystem.AppDataDirectory, FileName);

    /// <summary>Abre (criando na primeira vez) o banco e devolve um escopo de serviços para uma operação.</summary>
    public async Task<AsyncServiceScope> CreateScopeAsync(CancellationToken ct = default)
    {
        if (_provider is null)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (_provider is null)
                {
                    var provider = new ServiceCollection().AddLocalFinance(DatabasePath).BuildServiceProvider();
                    await provider.InitializeLocalAsync(ct);
                    _provider = provider;
                }
            }
            finally { _gate.Release(); }
        }
        return _provider.CreateAsyncScope();
    }

    /// <summary>Gera uma cópia íntegra do banco em um arquivo temporário (para backup/compartilhamento).</summary>
    public async Task<string> CreateBackupFileAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var target = Path.Combine(FileSystem.CacheDirectory, $"financas-backup-{DateTime.Now:yyyyMMdd-HHmm}.db3");
        if (File.Exists(target)) File.Delete(target);

        // VACUUM INTO gera uma cópia consistente mesmo com o banco em uso (caminho vai como parâmetro).
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {target}", ct);
        return target;
    }
}
