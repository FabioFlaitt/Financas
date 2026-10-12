using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Conexão com uma instituição financeira via provedor de Open Finance.
/// Nunca guarda senha bancária: apenas referências ao consentimento e tokens cifrados
/// (os tokens em si ficam no cofre de segredos da infraestrutura, não aqui).
/// </summary>
public class BankConnection : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    /// <summary>Instituição conectada (ex.: "Banco do Brasil").</summary>
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>Provedor/agregador utilizado (ex.: "Pluggy", "Belvo"). Veja docs/open-finance-provedores.md.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Identificador do item/conexão no provedor.</summary>
    public string? ProviderItemId { get; set; }

    /// <summary>Identificador do consentimento (Open Finance), nunca a senha do banco.</summary>
    public string? ConsentId { get; set; }

    public BankConnectionStatus Status { get; set; } = BankConnectionStatus.Pending;

    public DateTime? ConsentExpiresAtUtc { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }

    /// <summary>Indica que a conexão é apenas demonstrativa (dados fictícios identificados).</summary>
    public bool IsDemo { get; set; }

    public string? LastError { get; set; }

    public ICollection<BankSyncHistory> SyncHistory { get; set; } = new List<BankSyncHistory>();
}
