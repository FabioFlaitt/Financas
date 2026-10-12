namespace FinanceApp.Application.Banking;

/// <summary>
/// Abstração de um provedor/agregador de Open Finance. Permite trocar de fornecedor
/// (Pluggy, Belvo, etc.) sem reescrever as regras financeiras do sistema.
///
/// IMPORTANTE: nenhuma implementação pode solicitar ou armazenar a senha bancária do usuário.
/// O fluxo oficial é sempre de autorização/consentimento. Enquanto não houver um provedor real
/// configurado, usa-se a implementação demonstrativa (dados fictícios identificados).
/// </summary>
public interface IBankIntegrationProvider
{
    /// <summary>Nome do provedor (ex.: "Demo", "Pluggy"). Útil para logs e para a UI.</summary>
    string Name { get; }

    /// <summary>Indica se é um provedor de demonstração (dados fictícios).</summary>
    bool IsDemo { get; }

    /// <summary>Lista as instituições suportadas/participantes para o usuário escolher.</summary>
    Task<IReadOnlyList<BankInstitution>> GetInstitutionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Inicia o fluxo de conexão/consentimento. Retorna a URL de autorização (quando aplicável)
    /// e um identificador do consentimento. Nunca recebe senha bancária.
    /// </summary>
    Task<BankConnectResult> StartConnectionAsync(BankConnectRequest request, CancellationToken ct = default);

    /// <summary>Busca contas, cartões, saldos e transações conforme o consentimento concedido.</summary>
    Task<BankSyncResult> SyncAsync(string providerItemId, DateOnly fromDate, CancellationToken ct = default);

    /// <summary>Revoga o consentimento e encerra a conexão no provedor.</summary>
    Task DisconnectAsync(string providerItemId, CancellationToken ct = default);
}

public record BankInstitution(string Id, string Name, string? LogoUrl);

public record BankConnectRequest(Guid UserId, string InstitutionId);

public record BankConnectResult(string ProviderItemId, string? ConsentId, string? AuthorizationUrl, DateTime? ConsentExpiresAtUtc);

public record BankSyncResult(
    IReadOnlyList<ExternalAccount> Accounts,
    IReadOnlyList<ExternalTransaction> Transactions);

public record ExternalAccount(
    string ExternalId,
    string Name,
    string? Institution,
    decimal Balance,
    string Type);

public record ExternalTransaction(
    string ExternalId,
    string Description,
    decimal Amount,
    DateOnly Date,
    string? Merchant,
    bool IsCredit,
    string? AccountExternalId);
