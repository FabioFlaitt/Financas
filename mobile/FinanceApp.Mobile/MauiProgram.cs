using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using FinanceApp.Mobile.Views;

namespace FinanceApp.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()           // habilita o SpeechToText.Default
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Infraestrutura do app (uma instância por sessão)
        builder.Services.AddSingleton<LocalFinanceStore>();
        builder.Services.AddSingleton<FinanceApiClient>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<ISpeechToTextService, SpeechToTextService>();

        // --- Boas-vindas / criação do perfil local ---
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<WelcomePage>();

        // --- Ditar lançamento por voz ---
        builder.Services.AddTransient<VoiceExpenseViewModel>();
        builder.Services.AddTransient<VoiceExpensePage>();

        // --- Painel (aba) ---
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<DashboardPage>();

        // --- Gráficos / análises (aba) ---
        builder.Services.AddTransient<ChartsViewModel>();
        builder.Services.AddTransient<ChartsPage>();

        // --- Cartões e faturas (aba) ---
        builder.Services.AddTransient<CardsViewModel>();
        builder.Services.AddTransient<CardsPage>();
        builder.Services.AddTransient<CardEditViewModel>();
        builder.Services.AddTransient<CardEditPage>();
        builder.Services.AddTransient<CardInvoicesViewModel>();
        builder.Services.AddTransient<CardInvoicesPage>();
        builder.Services.AddTransient<InvoiceDetailViewModel>();
        builder.Services.AddTransient<InvoiceDetailPage>();
        builder.Services.AddTransient<InstallmentEditViewModel>();
        builder.Services.AddTransient<InstallmentEditPage>();

        // --- Configurações (aba hub) ---
        builder.Services.AddTransient<SettingsPage>();

        // --- Orçamento mensal ---
        builder.Services.AddTransient<BudgetViewModel>();
        builder.Services.AddTransient<BudgetPage>();
        builder.Services.AddTransient<BudgetEditViewModel>();
        builder.Services.AddTransient<BudgetEditPage>();

        // --- Importação de extratos ---
        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<ImportPage>();

        // --- Alertas e notificações ---
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<NotificationsPage>();

        // --- Lançamentos ---
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<TransactionEditViewModel>();
        builder.Services.AddTransient<TransactionEditPage>();

        // --- Contas ---
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<AccountEditViewModel>();
        builder.Services.AddTransient<AccountEditPage>();

        // --- Categorias ---
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<CategoryEditViewModel>();
        builder.Services.AddTransient<CategoryEditPage>();

        // --- Recorrências ---
        builder.Services.AddTransient<RecurringViewModel>();
        builder.Services.AddTransient<RecurringPage>();
        builder.Services.AddTransient<RecurringEditViewModel>();
        builder.Services.AddTransient<RecurringEditPage>();

        // --- Viagens (despesas de viagem com rota por GPS) ---
        builder.Services.AddTransient<TripsViewModel>();
        builder.Services.AddTransient<TripsPage>();
        builder.Services.AddTransient<TripStartViewModel>();
        builder.Services.AddTransient<TripStartPage>();
        builder.Services.AddTransient<TripDetailViewModel>();
        builder.Services.AddTransient<TripDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
