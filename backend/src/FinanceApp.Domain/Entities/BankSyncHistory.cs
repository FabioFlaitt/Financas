using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Registro de cada tentativa de sincronização de uma <see cref="BankConnection"/>,
/// para auditoria e exibição da "última sincronização".
/// </summary>
public class BankSyncHistory : BaseEntity
{
    public Guid BankConnectionId { get; set; }
    public BankConnection? BankConnection { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public SyncStatus Status { get; set; }

    /// <summary>Quantidade de lançamentos importados nesta sincronização.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Quantidade de lançamentos ignorados por já existirem (idempotência).</summary>
    public int SkippedDuplicateCount { get; set; }

    public string? Message { get; set; }
}
