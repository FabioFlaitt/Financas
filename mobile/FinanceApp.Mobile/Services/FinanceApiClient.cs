using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Services;

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}

/// <summary>Cliente HTTP tipado para a API do Meu Controle Financeiro.</summary>
public class FinanceApiClient
{
    private readonly HttpClient _http;
    private readonly SessionService _session;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FinanceApiClient(HttpClient http, SessionService session)
    {
        _http = http;
        _session = session;
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _session.IsAuthenticated
            ? new AuthenticationHeaderValue("Bearer", _session.AccessToken)
            : null;
    }

    private static Uri BuildUri(string path) => new(new Uri(ApiConfig.BaseUrl), path);

    // ---- Auth ----

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(BuildUri("/api/auth/login"), new { email, password }, JsonOptions, ct);
        return await ReadAsync<AuthResult>(response, ct);
    }

    public async Task<AuthResult> RegisterAsync(string name, string email, string password, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(BuildUri("/api/auth/register"), new { name, email, password }, JsonOptions, ct);
        return await ReadAsync<AuthResult>(response, ct);
    }

    // ---- Dashboard ----

    public Task<DashboardResult> GetDashboardAsync(int? year = null, int? month = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (year is int y) query.Add($"year={y}");
        if (month is int m) query.Add($"month={m}");
        var path = "/api/dashboard" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return GetAsync<DashboardResult>(path, ct);
    }

    // ---- Transações ----

    public Task<PagedResult<TransactionDto>> ListTransactionsAsync(int? year, int? month, string? search, CancellationToken ct = default)
    {
        var query = new List<string> { "pageSize=100" };
        if (year is int y) query.Add($"year={y}");
        if (month is int m) query.Add($"month={m}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        return GetAsync<PagedResult<TransactionDto>>("/api/transactions?" + string.Join("&", query), ct);
    }

    public Task<TransactionDto> CreateTransactionAsync(object body, CancellationToken ct = default)
        => SendAsync<TransactionDto>(HttpMethod.Post, "/api/transactions", body, ct);

    public Task<TransactionDto> UpdateTransactionAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<TransactionDto>(HttpMethod.Put, $"/api/transactions/{id}", body, ct);

    public Task DeleteTransactionAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/transactions/{id}", null, ct);

    // ---- Contas ----

    public Task<List<AccountDto>> ListAccountsAsync(CancellationToken ct = default)
        => GetAsync<List<AccountDto>>("/api/accounts", ct);

    public Task<AccountDto> CreateAccountAsync(object body, CancellationToken ct = default)
        => SendAsync<AccountDto>(HttpMethod.Post, "/api/accounts", body, ct);

    public Task<AccountDto> UpdateAccountAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<AccountDto>(HttpMethod.Put, $"/api/accounts/{id}", body, ct);

    public Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/accounts/{id}", null, ct);

    // ---- Categorias ----

    public Task<List<CategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
        => GetAsync<List<CategoryDto>>("/api/categories", ct);

    public Task<CategoryDto> CreateCategoryAsync(object body, CancellationToken ct = default)
        => SendAsync<CategoryDto>(HttpMethod.Post, "/api/categories", body, ct);

    public Task<CategoryDto> UpdateCategoryAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<CategoryDto>(HttpMethod.Put, $"/api/categories/{id}", body, ct);

    public Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/categories/{id}", null, ct);

    // ---- Recorrências ----

    public Task<List<RecurringTransactionDto>> ListRecurringAsync(CancellationToken ct = default)
        => GetAsync<List<RecurringTransactionDto>>("/api/recurrences", ct);

    public Task<RecurringTransactionDto> CreateRecurringAsync(object body, CancellationToken ct = default)
        => SendAsync<RecurringTransactionDto>(HttpMethod.Post, "/api/recurrences", body, ct);

    public Task<RecurringTransactionDto> UpdateRecurringAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<RecurringTransactionDto>(HttpMethod.Put, $"/api/recurrences/{id}", body, ct);

    public Task DeleteRecurringAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/recurrences/{id}", null, ct);

    public Task GenerateRecurringAsync(CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/recurrences/generate", new { }, ct);

    // ---- Relatórios ----

    public Task<ReportSummary> GetReportSummaryAsync(int? year = null, int? month = null, int months = 6, CancellationToken ct = default)
    {
        var query = new List<string> { $"months={months}" };
        if (year is int y) query.Add($"year={y}");
        if (month is int m) query.Add($"month={m}");
        return GetAsync<ReportSummary>("/api/reports/summary?" + string.Join("&", query), ct);
    }

    // ---- Orçamentos ----

    public Task<BudgetOverview> GetBudgetsAsync(int? year = null, int? month = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (year is int y) query.Add($"year={y}");
        if (month is int m) query.Add($"month={m}");
        var path = "/api/budgets" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return GetAsync<BudgetOverview>(path, ct);
    }

    public Task<BudgetItem> CreateBudgetAsync(object body, CancellationToken ct = default)
        => SendAsync<BudgetItem>(HttpMethod.Post, "/api/budgets", body, ct);

    public Task<BudgetItem> UpdateBudgetAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<BudgetItem>(HttpMethod.Put, $"/api/budgets/{id}", body, ct);

    public Task DeleteBudgetAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/budgets/{id}", null, ct);

    // ---- Cartões e faturas ----

    public Task<List<CardDto>> ListCardsAsync(CancellationToken ct = default)
        => GetAsync<List<CardDto>>("/api/cards", ct);

    public Task<CardDto> CreateCardAsync(object body, CancellationToken ct = default)
        => SendAsync<CardDto>(HttpMethod.Post, "/api/cards", body, ct);

    public Task<CardDto> UpdateCardAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<CardDto>(HttpMethod.Put, $"/api/cards/{id}", body, ct);

    public Task DeleteCardAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/cards/{id}", null, ct);

    public Task<List<InvoiceListItem>> ListCardInvoicesAsync(Guid cardId, CancellationToken ct = default)
        => GetAsync<List<InvoiceListItem>>($"/api/cards/{cardId}/invoices", ct);

    public Task AddInstallmentAsync(Guid cardId, object body, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, $"/api/cards/{cardId}/installments", body, ct);

    public Task<InvoiceDetail> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => GetAsync<InvoiceDetail>($"/api/invoices/{invoiceId}", ct);

    public Task<InvoiceListItem> CloseInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => SendAsync<InvoiceListItem>(HttpMethod.Post, $"/api/invoices/{invoiceId}/close", new { }, ct);

    public Task<InvoiceListItem> PayInvoiceAsync(Guid invoiceId, object body, CancellationToken ct = default)
        => SendAsync<InvoiceListItem>(HttpMethod.Post, $"/api/invoices/{invoiceId}/pay", body, ct);

    // ---- Importação de extratos ----

    public Task<ImportPreview> PreviewImportAsync(object body, CancellationToken ct = default)
        => SendAsync<ImportPreview>(HttpMethod.Post, "/api/imports/preview", body, ct);

    public Task<ImportResult> CommitImportAsync(object body, CancellationToken ct = default)
        => SendAsync<ImportResult>(HttpMethod.Post, "/api/imports/commit", body, ct);

    // ---- Viagens ----

    public Task<List<TripDto>> ListTripsAsync(CancellationToken ct = default)
        => GetAsync<List<TripDto>>("/api/trips", ct);

    public Task<TripDetailDto> GetTripAsync(Guid id, CancellationToken ct = default)
        => GetAsync<TripDetailDto>($"/api/trips/{id}", ct);

    public Task<TripDetailDto> StartTripAsync(object body, CancellationToken ct = default)
        => SendAsync<TripDetailDto>(HttpMethod.Post, "/api/trips", body, ct);

    public Task<TripDetailDto> FinishTripAsync(Guid id, object body, CancellationToken ct = default)
        => SendAsync<TripDetailDto>(HttpMethod.Post, $"/api/trips/{id}/finish", body, ct);

    public Task DeleteTripAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/trips/{id}", null, ct);

    public Task<TripExpenseDto> AddTripExpenseAsync(Guid tripId, object body, CancellationToken ct = default)
        => SendAsync<TripExpenseDto>(HttpMethod.Post, $"/api/trips/{tripId}/expenses", body, ct);

    public Task DeleteTripExpenseAsync(Guid tripId, Guid expenseId, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"/api/trips/{tripId}/expenses/{expenseId}", null, ct);

    public Task<TripSummary> AddTripLocationAsync(Guid tripId, object body, CancellationToken ct = default)
        => SendAsync<TripSummary>(HttpMethod.Post, $"/api/trips/{tripId}/locations", body, ct);

    // ---- infra ----

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        ApplyAuth();
        var response = await _http.GetAsync(BuildUri(path), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        ApplyAuth();
        using var request = new HttpRequestMessage(method, BuildUri(path));
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        ApplyAuth();
        using var request = new HttpRequestMessage(method, BuildUri(path));
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException($"Falha na requisição ({(int)response.StatusCode}).");
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ApiException($"Falha na requisição ({(int)response.StatusCode}). {body}");
        }
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new ApiException("Resposta vazia da API.");
    }
}
