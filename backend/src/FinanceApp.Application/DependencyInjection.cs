using FinanceApp.Application.Accounts;
using FinanceApp.Application.Auth;
using FinanceApp.Application.Budgets;
using FinanceApp.Application.Cards;
using FinanceApp.Application.Categories;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Imports;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Recurrences;
using FinanceApp.Application.Reports;
using FinanceApp.Application.Transactions;
using FinanceApp.Application.Trips;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Application;

public static class DependencyInjection
{
    /// <summary>Registra os casos de uso/serviços da camada de aplicação.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IStatementImportService, StatementImportService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IAlertsService, AlertsService>();
        return services;
    }
}
