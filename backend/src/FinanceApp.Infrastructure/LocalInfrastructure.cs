using FinanceApp.Application;
using FinanceApp.Application.Categories;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Common;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Infrastructure;

/// <summary>Usuário único do modo local: o dono do aparelho (sem login e sem token).</summary>
public sealed class LocalCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
}

public static class LocalInfrastructure
{
    public const string LocalUserEmail = "local@financas.app";

    /// <summary>
    /// Compõe as regras de negócio (camada Application) sobre um arquivo SQLite no aparelho,
    /// sem servidor, sem JWT e sem rede. Os mesmos serviços do backend, executando localmente.
    /// </summary>
    public static IServiceCollection AddLocalFinance(this IServiceCollection services, string databasePath)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<LocalCurrentUser>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<LocalCurrentUser>());
        services.AddApplication();
        return services;
    }

    /// <summary>
    /// Garante que o banco existe e, se já houver um perfil salvo, vincula o <see cref="LocalCurrentUser"/>.
    /// NÃO cria perfil automaticamente — isso é responsabilidade da tela de boas-vindas.
    /// Como efeito colateral: se o único usuário for o perfil "Meu perfil" criado automaticamente pela versão
    /// antiga do app e sem nenhum dado financeiro, ele é apagado para forçar o fluxo de boas-vindas.
    /// </summary>
    public static async Task EnsureDatabaseAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        await EnsureLocalColumnsAsync(db, ct);

        var users = await db.Users.OrderBy(u => u.CreatedAtUtc).ToListAsync(ct);
        if (users.Count == 1 && users[0].Email == LocalUserEmail && users[0].Name == "Meu perfil")
        {
            var legacyId = users[0].Id;
            var hasData =
                await db.Transactions.AnyAsync(t => t.UserId == legacyId, ct) ||
                await db.Accounts.AnyAsync(a => a.UserId == legacyId, ct) ||
                await db.CreditCards.AnyAsync(c => c.UserId == legacyId, ct);
            if (!hasData)
            {
                var cats = await db.Categories.Where(c => c.UserId == legacyId).ToListAsync(ct);
                var prefs = await db.NotificationPreferences.Where(p => p.UserId == legacyId).ToListAsync(ct);
                db.Categories.RemoveRange(cats);
                db.NotificationPreferences.RemoveRange(prefs);
                db.Users.Remove(users[0]);
                await db.SaveChangesAsync(ct);
                users.Clear();
            }
        }

        provider.GetRequiredService<LocalCurrentUser>().UserId = users.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// Migração aditiva leve para o SQLite local (que usa EnsureCreated, sem migrations): adiciona colunas
    /// novas em tabelas já existentes, de forma idempotente, para não quebrar instalações antigas.
    /// </summary>
    private static async Task EnsureLocalColumnsAsync(AppDbContext db, CancellationToken ct)
    {
        var additions = new (string Table, string Column, string Type)[]
        {
            ("Trips", "OriginLatitude", "REAL"),
            ("Trips", "OriginLongitude", "REAL"),
            ("Trips", "DestinationLatitude", "REAL"),
            ("Trips", "DestinationLongitude", "REAL"),
            ("Trips", "IsDeleted", "INTEGER NOT NULL DEFAULT 0"),
        };

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);
        try
        {
            foreach (var group in additions.GroupBy(a => a.Table))
            {
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = $"PRAGMA table_info({group.Key});";
                    await using var reader = await pragma.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct)) existing.Add(reader.GetString(1)); // coluna 1 = name
                }
                if (existing.Count == 0) continue; // tabela não existe (nada a migrar)

                foreach (var (table, column, type) in group)
                {
                    if (existing.Contains(column)) continue;
                    await using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type};";
                    await alter.ExecuteNonQueryAsync(ct);
                }
            }
        }
        catch { /* se falhar, segue; EnsureCreated já cobre instalações novas */ }
        finally { if (!wasOpen) await conn.CloseAsync(); }
    }

    /// <summary>Retorna <c>true</c> se já existe um perfil no banco local.</summary>
    public static async Task<bool> HasProfileAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AnyAsync(ct);
    }

    /// <summary>Nome do perfil atual, ou <c>null</c> se ainda não houver um.</summary>
    public static async Task<string?> GetProfileNameAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return u?.Name;
    }

    /// <summary>
    /// Cria o perfil local com <paramref name="name"/> (e opcionalmente <paramref name="email"/>), semeia as
    /// categorias e preferências de notificação padrão e vincula o usuário atual.
    /// </summary>
    public static async Task CreateProfileAsync(this IServiceProvider provider, string name, string? email = null, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Users.AnyAsync(ct))
            throw new InvalidOperationException("Já existe um perfil local neste aparelho.");

        var user = new User
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Meu perfil" : name.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? LocalUserEmail : email!.Trim().ToLowerInvariant(),
            PasswordHash = string.Empty
        };
        db.Users.Add(user);
        foreach (var category in DefaultCategories.CreateFor(user.Id))
            db.Categories.Add(category);
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
            db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Type = type });
        await db.SaveChangesAsync(ct);

        provider.GetRequiredService<LocalCurrentUser>().UserId = user.Id;
    }

    /// <summary>
    /// Helper usado por testes e pela composição legada: garante banco + perfil padrão em um único passo.
    /// Em produção o app usa <see cref="EnsureDatabaseAsync"/> + <see cref="CreateProfileAsync"/>.
    /// </summary>
    public static async Task InitializeLocalAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        await provider.EnsureDatabaseAsync(ct);
        if (!await provider.HasProfileAsync(ct))
            await provider.CreateProfileAsync("Meu perfil", LocalUserEmail, ct);
    }
}
