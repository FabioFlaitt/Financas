using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceApp.Mobile.Models;
using FinanceApp.Application.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Acc = FinanceApp.Application.Accounts;
using Bud = FinanceApp.Application.Budgets;
using Cat = FinanceApp.Application.Categories;
using Crd = FinanceApp.Application.Cards;
using Dash = FinanceApp.Application.Dashboard;
using Imp = FinanceApp.Application.Imports;
using Notif = FinanceApp.Application.Notifications;
using Rec = FinanceApp.Application.Recurrences;
using Rep = FinanceApp.Application.Reports;
using Trp = FinanceApp.Application.Trips;
using Trx = FinanceApp.Application.Transactions;

namespace FinanceApp.Mobile.Services;

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}

/// <summary>
/// Fachada de dados do app. Mantém a superfície que as telas já usavam, mas executa as regras de negócio
/// LOCALMENTE (banco SQLite no aparelho) em vez de chamar um servidor. Os modelos das telas são preenchidos
/// por mapeamento JSON a partir dos DTOs da camada de aplicação.
/// </summary>
public class FinanceApiClient
{
    private readonly LocalFinanceStore _store;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FinanceApiClient(LocalFinanceStore store) => _store = store;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---- Painel ----

    public Task<DashboardResult> GetDashboardAsync(int? year = null, int? month = null, CancellationToken ct = default)
        => Using<Dash.IDashboardService>().Run(async s => Map<DashboardResult>(await s.GetAsync(
            new Dash.DashboardQuery { Year = year ?? Today.Year, Month = month ?? Today.Month }, ct)));

    // ---- Transações ----

    public Task<PagedResult<TransactionDto>> ListTransactionsAsync(int? year, int? month, string? search, CancellationToken ct = default)
        => Using<Trx.ITransactionService>().Run(async s => Map<PagedResult<TransactionDto>>(await s.ListAsync(
            new Trx.TransactionListQuery { Year = year, Month = month, Search = search, PageSize = 100 }, ct)));

    public Task<TransactionDto> CreateTransactionAsync(object body, CancellationToken ct = default)
        => Using<Trx.ITransactionService>().Run(async s => Map<TransactionDto>(await s.CreateAsync(Map<Trx.CreateTransactionRequest>(body), ct)));

    public Task<TransactionDto> UpdateTransactionAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Trx.ITransactionService>().Run(async s => Map<TransactionDto>(await s.UpdateAsync(Map<Trx.UpdateTransactionRequest>(body) with { Id = id }, ct)));

    public Task DeleteTransactionAsync(Guid id, CancellationToken ct = default)
        => Using<Trx.ITransactionService>().Run(s => s.DeleteAsync(id, ct));

    // ---- Contas ----

    public Task<List<AccountDto>> ListAccountsAsync(CancellationToken ct = default)
        => Using<Acc.IAccountService>().Run(async s => Map<List<AccountDto>>(await s.ListAsync(ct)));

    public Task<AccountDto> CreateAccountAsync(object body, CancellationToken ct = default)
        => Using<Acc.IAccountService>().Run(async s => Map<AccountDto>(await s.CreateAsync(Map<Acc.SaveAccountRequest>(body), ct)));

    public Task<AccountDto> UpdateAccountAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Acc.IAccountService>().Run(async s => Map<AccountDto>(await s.UpdateAsync(id, Map<Acc.SaveAccountRequest>(body), ct)));

    public Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
        => Using<Acc.IAccountService>().Run(s => s.DeleteAsync(id, ct));

    // ---- Categorias ----

    public Task<List<CategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
        => Using<Cat.ICategoryService>().Run(async s => Map<List<CategoryDto>>(await s.ListAsync(ct)));

    public Task<CategoryDto> CreateCategoryAsync(object body, CancellationToken ct = default)
        => Using<Cat.ICategoryService>().Run(async s => Map<CategoryDto>(await s.CreateAsync(Map<Cat.SaveCategoryRequest>(body), ct)));

    public Task<CategoryDto> UpdateCategoryAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Cat.ICategoryService>().Run(async s => Map<CategoryDto>(await s.UpdateAsync(id, Map<Cat.SaveCategoryRequest>(body), ct)));

    public Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
        => Using<Cat.ICategoryService>().Run(s => s.DeleteAsync(id, ct));

    // ---- Recorrências ----

    public Task<List<RecurringTransactionDto>> ListRecurringAsync(CancellationToken ct = default)
        => Using<Rec.IRecurringTransactionService>().Run(async s => Map<List<RecurringTransactionDto>>(await s.ListAsync(ct)));

    public Task<RecurringTransactionDto> CreateRecurringAsync(object body, CancellationToken ct = default)
        => Using<Rec.IRecurringTransactionService>().Run(async s => Map<RecurringTransactionDto>(await s.CreateAsync(Map<Rec.SaveRecurringTransactionRequest>(body), ct)));

    public Task<RecurringTransactionDto> UpdateRecurringAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Rec.IRecurringTransactionService>().Run(async s => Map<RecurringTransactionDto>(await s.UpdateAsync(id, Map<Rec.SaveRecurringTransactionRequest>(body), ct)));

    public Task DeleteRecurringAsync(Guid id, CancellationToken ct = default)
        => Using<Rec.IRecurringTransactionService>().Run(s => s.DeleteAsync(id, ct));

    public Task GenerateRecurringAsync(CancellationToken ct = default)
        => Using<Rec.IRecurringTransactionService>().Run(s => s.GenerateDueAsync(ct));

    // ---- Relatórios ----

    public Task<ReportSummary> GetReportSummaryAsync(int? year = null, int? month = null, int months = 6, CancellationToken ct = default)
        => Using<Rep.IReportService>().Run(async s => Map<ReportSummary>(await s.GetSummaryAsync(
            year ?? Today.Year, month ?? Today.Month, months, ct)));

    // ---- Orçamentos ----

    public Task<BudgetOverview> GetBudgetsAsync(int? year = null, int? month = null, CancellationToken ct = default)
        => Using<Bud.IBudgetService>().Run(async s => Map<BudgetOverview>(await s.GetOverviewAsync(
            year ?? Today.Year, month ?? Today.Month, ct)));

    public Task<BudgetItem> CreateBudgetAsync(object body, CancellationToken ct = default)
        => Using<Bud.IBudgetService>().Run(async s => Map<BudgetItem>(await s.CreateAsync(Map<Bud.SaveBudgetRequest>(body), ct)));

    public Task<BudgetItem> UpdateBudgetAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Bud.IBudgetService>().Run(async s => Map<BudgetItem>(await s.UpdateAsync(id, Map<Bud.SaveBudgetRequest>(body), ct)));

    public Task DeleteBudgetAsync(Guid id, CancellationToken ct = default)
        => Using<Bud.IBudgetService>().Run(s => s.DeleteAsync(id, ct));

    // ---- Cartões e faturas ----

    public Task<List<CardDto>> ListCardsAsync(CancellationToken ct = default)
        => Using<Crd.ICardService>().Run(async s => Map<List<CardDto>>(await s.ListAsync(ct)));

    public Task<CardDto> CreateCardAsync(object body, CancellationToken ct = default)
        => Using<Crd.ICardService>().Run(async s => Map<CardDto>(await s.CreateAsync(Map<Crd.SaveCardRequest>(body), ct)));

    public Task<CardDto> UpdateCardAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Crd.ICardService>().Run(async s => Map<CardDto>(await s.UpdateAsync(id, Map<Crd.SaveCardRequest>(body), ct)));

    public Task DeleteCardAsync(Guid id, CancellationToken ct = default)
        => Using<Crd.ICardService>().Run(s => s.DeleteAsync(id, ct));

    public Task<List<InvoiceListItem>> ListCardInvoicesAsync(Guid cardId, CancellationToken ct = default)
        => Using<Crd.IInvoiceService>().Run(async s => Map<List<InvoiceListItem>>(await s.ListByCardAsync(cardId, ct)));

    public Task AddInstallmentAsync(Guid cardId, object body, CancellationToken ct = default)
        => Using<Crd.IInvoiceService>().Run(s => s.AddInstallmentAsync(Map<Crd.AddInstallmentRequest>(body) with { CreditCardId = cardId }, ct));

    public Task<InvoiceDetail> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => Using<Crd.IInvoiceService>().Run(async s => Map<InvoiceDetail>(await s.GetAsync(invoiceId, ct)));

    public Task<InvoiceListItem> CloseInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => Using<Crd.IInvoiceService>().Run(async s => Map<InvoiceListItem>(await s.CloseAsync(invoiceId, ct)));

    public Task<InvoiceListItem> PayInvoiceAsync(Guid invoiceId, object body, CancellationToken ct = default)
        => Using<Crd.IInvoiceService>().Run(async s => Map<InvoiceListItem>(await s.PayAsync(invoiceId, Map<Crd.PayInvoiceRequest>(body), ct)));

    // ---- Notificações e alertas ----

    public Task<List<AlertItem>> GetAlertsAsync(CancellationToken ct = default)
        => Using<Notif.IAlertsService>().Run(async s => Map<List<AlertItem>>(await s.GetAsync(ct)));

    public Task<List<NotificationPreferenceItem>> GetNotificationPreferencesAsync(CancellationToken ct = default)
        => Using<Notif.INotificationPreferenceService>().Run(async s => Map<List<NotificationPreferenceItem>>(await s.ListAsync(ct)));

    public Task<NotificationPreferenceItem> UpdateNotificationPreferenceAsync(string type, object body, CancellationToken ct = default)
        => Using<Notif.INotificationPreferenceService>().Run(async s =>
        {
            if (!Enum.TryParse<FinanceApp.Domain.Enums.NotificationType>(type, ignoreCase: true, out var parsed))
                throw new ApiException("Tipo de notificação inválido.");
            return Map<NotificationPreferenceItem>(await s.UpdateAsync(parsed, Map<Notif.UpdateNotificationPreferenceRequest>(body), ct));
        });

    // ---- Importação de extratos ----

    public Task<ImportPreview> PreviewImportAsync(object body, CancellationToken ct = default)
        => Using<Imp.IStatementImportService>().Run(async s => Map<ImportPreview>(await s.PreviewAsync(Map<Imp.ImportPreviewRequest>(body), ct)));

    public Task<ImportResult> CommitImportAsync(object body, CancellationToken ct = default)
        => Using<Imp.IStatementImportService>().Run(async s => Map<ImportResult>(await s.CommitAsync(Map<Imp.ImportCommitRequest>(body), ct)));

    // ---- Viagens ----

    public Task<List<TripDto>> ListTripsAsync(CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<List<TripDto>>(await s.ListAsync(ct)));

    public Task<TripDetailDto> GetTripAsync(Guid id, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<TripDetailDto>(await s.GetAsync(id, ct)));

    public Task<TripDetailDto> StartTripAsync(object body, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<TripDetailDto>(await s.StartAsync(Map<Trp.StartTripRequest>(body), ct)));

    public Task<TripDetailDto> FinishTripAsync(Guid id, object body, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<TripDetailDto>(await s.FinishAsync(id, Map<Trp.FinishTripRequest>(body), ct)));

    public Task DeleteTripAsync(Guid id, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(s => s.DeleteAsync(id, ct));

    public Task<TripExpenseDto> AddTripExpenseAsync(Guid tripId, object body, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<TripExpenseDto>(await s.AddExpenseAsync(tripId, Map<Trp.AddTripExpenseRequest>(body), ct)));

    public Task DeleteTripExpenseAsync(Guid tripId, Guid expenseId, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(s => s.DeleteExpenseAsync(tripId, expenseId, ct));

    public Task<TripSummary> AddTripLocationAsync(Guid tripId, object body, CancellationToken ct = default)
        => Using<Trp.ITripService>().Run(async s => Map<TripSummary>(await s.AddLocationPointAsync(tripId, Map<Trp.AddLocationPointRequest>(body), ct)));

    // ---- infra ----

    private ServiceCall<TService> Using<TService>() where TService : notnull => new(_store);

    /// <summary>Resolve o serviço em um escopo próprio (um DbContext por operação) e o descarta ao final.</summary>
    private readonly struct ServiceCall<TService> where TService : notnull
    {
        private readonly LocalFinanceStore _store;

        public ServiceCall(LocalFinanceStore store) => _store = store;

        public async Task<TResult> Run<TResult>(Func<TService, Task<TResult>> action)
        {
            await using var scope = await _store.CreateScopeAsync();
            try { return await action(scope.ServiceProvider.GetRequiredService<TService>()); }
            catch (AppValidationException ex) { throw Friendly(ex); }
        }

        public async Task Run(Func<TService, Task> action)
        {
            await using var scope = await _store.CreateScopeAsync();
            try { await action(scope.ServiceProvider.GetRequiredService<TService>()); }
            catch (AppValidationException ex) { throw Friendly(ex); }
        }

        /// <summary>Troca a mensagem genérica de validação pelos motivos reais, prontos para exibir.</summary>
        private static ApiException Friendly(AppValidationException ex)
            => new(string.Join(" ", ex.Errors.SelectMany(e => e.Value)));
    }

    private static T Map<T>(object? value)
    {
        var json = JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new ApiException("Resposta vazia.");
    }
}
