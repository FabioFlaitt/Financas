using FinanceApp.Application.Banking;

namespace FinanceApp.Infrastructure.Banking;

/// <summary>
/// Implementação DEMONSTRATIVA de <see cref="IBankIntegrationProvider"/>. Não acessa nenhum
/// banco real: gera dados fictícios, claramente identificados, para permitir testar o fluxo de
/// conexão/sincronização enquanto um provedor real de Open Finance (ex.: Pluggy, Belvo) não está
/// configurado. Nunca solicita ou armazena senha bancária.
/// Veja docs/open-finance-provedores.md para os fornecedores a avaliar.
/// </summary>
public class DemoBankIntegrationProvider : IBankIntegrationProvider
{
    public string Name => "Demo";
    public bool IsDemo => true;

    private static readonly IReadOnlyList<BankInstitution> Institutions = new List<BankInstitution>
    {
        new("demo-itau",      "Itaú (demonstração)",            null),
        new("demo-bb",        "Banco do Brasil (demonstração)", null),
        new("demo-bradesco",  "Bradesco (demonstração)",        null),
        new("demo-santander", "Santander (demonstração)",       null),
        new("demo-caixa",     "Caixa (demonstração)",           null),
        new("demo-nubank",    "Nubank (demonstração)",          null),
    };

    public Task<IReadOnlyList<BankInstitution>> GetInstitutionsAsync(CancellationToken ct = default)
        => Task.FromResult(Institutions);

    public Task<BankConnectResult> StartConnectionAsync(BankConnectRequest request, CancellationToken ct = default)
    {
        var itemId = $"demo-item-{Guid.NewGuid():N}";
        // Sem URL de autorização real: no modo demo a conexão é concluída imediatamente.
        return Task.FromResult(new BankConnectResult(
            ProviderItemId: itemId,
            ConsentId: $"demo-consent-{Guid.NewGuid():N}",
            AuthorizationUrl: null,
            ConsentExpiresAtUtc: DateTime.UtcNow.AddDays(90)));
    }

    public Task<BankSyncResult> SyncAsync(string providerItemId, DateOnly fromDate, CancellationToken ct = default)
    {
        var accountExternalId = $"{providerItemId}-acc";
        var accounts = new List<ExternalAccount>
        {
            new(accountExternalId, "Conta corrente (demonstração)", "Banco demonstração", 4200.55m, "Checking")
        };

        // Lançamentos fictícios dos últimos dias, identificados como demonstração.
        var rnd = new Random(providerItemId.GetHashCode());
        var merchants = new[] { "Supermercado Demo", "Posto Demo", "Streaming Demo", "Farmácia Demo", "Restaurante Demo" };
        var transactions = new List<ExternalTransaction>();
        for (int i = 0; i < 8; i++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-i * 2);
            if (date < fromDate) break;
            var merchant = merchants[rnd.Next(merchants.Length)];
            var amount = Math.Round((decimal)(rnd.NextDouble() * 180 + 15), 2);
            transactions.Add(new ExternalTransaction(
                ExternalId: $"{providerItemId}-tx-{i}",
                Description: $"[DEMO] {merchant}",
                Amount: amount,
                Date: date,
                Merchant: merchant,
                IsCredit: false,
                AccountExternalId: accountExternalId));
        }

        return Task.FromResult(new BankSyncResult(accounts, transactions));
    }

    public Task DisconnectAsync(string providerItemId, CancellationToken ct = default)
        => Task.CompletedTask;
}
