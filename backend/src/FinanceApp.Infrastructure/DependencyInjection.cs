using FinanceApp.Application.Banking;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Infrastructure.Banking;
using FinanceApp.Infrastructure.Common;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=financas;Username=financas;Password=financas";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Provedor de Open Finance: por enquanto o demonstrativo. Trocar aqui por um
        // provedor real (Pluggy/Belvo) sem alterar as regras financeiras do sistema.
        services.AddScoped<IBankIntegrationProvider, DemoBankIntegrationProvider>();

        services.AddScoped<DemoDataSeeder>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        return services;
    }
}
