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
    /// Cria o banco (se não existir) e o perfil local com categorias e preferências padrão.
    /// Idempotente: pode ser chamado a cada abertura do app.
    /// </summary>
    public static async Task InitializeLocalAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        var user = await db.Users.OrderBy(u => u.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (user is null)
        {
            user = new User { Name = "Meu perfil", Email = LocalUserEmail, PasswordHash = string.Empty };
            db.Users.Add(user);
            foreach (var category in DefaultCategories.CreateFor(user.Id))
                db.Categories.Add(category);
            foreach (NotificationType type in Enum.GetValues<NotificationType>())
                db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Type = type });
            await db.SaveChangesAsync(ct);
        }

        provider.GetRequiredService<LocalCurrentUser>().UserId = user.Id;
    }
}
