using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Preferência de notificação do usuário para cada <see cref="NotificationType"/>.
/// </summary>
public class NotificationPreference : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Oculta valores/detalhes sensíveis na tela bloqueada (padrão: oculta).</summary>
    public bool HideSensitiveOnLockScreen { get; set; } = true;

    /// <summary>Horário preferido para notificações de resumo (formato HH:mm).</summary>
    public string? PreferredTime { get; set; }
}
