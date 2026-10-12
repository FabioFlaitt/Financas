namespace FinanceApp.Domain.Common;

/// <summary>
/// Entidade base com identificador e carimbos de auditoria em UTC.
/// Todos os agregados herdam daqui para garantir consistência de chaves e datas.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Momento de criação (sempre UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Momento da última atualização (sempre UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Marca entidades que pertencem a um usuário. Usado para isolar dados por usuário
/// (nunca expor dados de um usuário para outro).
/// </summary>
public interface IUserOwnedEntity
{
    Guid UserId { get; set; }
}
