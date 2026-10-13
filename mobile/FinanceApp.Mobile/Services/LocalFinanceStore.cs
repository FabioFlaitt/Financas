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

    private async Task<ServiceProvider> EnsureProviderAsync(CancellationToken ct)
    {
        if (_provider is not null) return _provider;
        await _gate.WaitAsync(ct);
        try
        {
            if (_provider is null)
            {
                var provider = new ServiceCollection().AddLocalFinance(DatabasePath).BuildServiceProvider();
                await provider.EnsureDatabaseAsync(ct);
                _provider = provider;
            }
            return _provider;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Abre (criando na primeira vez) o banco e devolve um escopo de serviços para uma operação.</summary>
    public async Task<AsyncServiceScope> CreateScopeAsync(CancellationToken ct = default)
    {
        var provider = await EnsureProviderAsync(ct);
        return provider.CreateAsyncScope();
    }

    /// <summary>Retorna <c>true</c> se o aparelho já tem um perfil criado.</summary>
    public async Task<bool> HasProfileAsync(CancellationToken ct = default)
    {
        var provider = await EnsureProviderAsync(ct);
        return await provider.HasProfileAsync(ct);
    }

    /// <summary>Nome do perfil atual (ou <c>null</c> na primeira abertura).</summary>
    public async Task<string?> GetProfileNameAsync(CancellationToken ct = default)
    {
        var provider = await EnsureProviderAsync(ct);
        return await provider.GetProfileNameAsync(ct);
    }

    /// <summary>Cria o perfil local com nome (e opcionalmente e-mail). Falha se já existir um perfil.</summary>
    public async Task CreateProfileAsync(string name, string? email = null, CancellationToken ct = default)
    {
        var provider = await EnsureProviderAsync(ct);
        await provider.CreateProfileAsync(name, email, ct);
    }

    /// <summary>
    /// Apaga TUDO: fecha o provider, remove o arquivo do banco (e logs do SQLite) e deixa o app pronto
    /// para a tela de boas-vindas criar um novo perfil. Chamada só após confirmação explícita do usuário.
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_provider is not null)
            {
                await _provider.DisposeAsync();
                _provider = null;
            }
            // O SQLite usa connection pool: fechar as conexões antes de deletar o arquivo.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            {
                var path = DatabasePath + suffix;
                try { if (File.Exists(path)) File.Delete(path); } catch { /* ignora: será recriado */ }
            }
        }
        finally { _gate.Release(); }
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
