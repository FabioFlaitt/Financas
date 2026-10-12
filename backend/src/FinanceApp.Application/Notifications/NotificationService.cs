using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Notifications;

public record NotificationPreferenceDto
{
    public NotificationType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public bool HideSensitiveOnLockScreen { get; init; }
    public string? PreferredTime { get; init; }
}

public record UpdateNotificationPreferenceRequest
{
    public bool Enabled { get; init; }
    public bool HideSensitiveOnLockScreen { get; init; }
    public string? PreferredTime { get; init; }
}

public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceDto>> ListAsync(CancellationToken ct = default);
    Task<NotificationPreferenceDto> UpdateAsync(NotificationType type, UpdateNotificationPreferenceRequest request, CancellationToken ct = default);
}

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotificationPreferenceService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<NotificationPreferenceDto>> ListAsync(CancellationToken ct = default)
    {
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == UserId)
            .ToListAsync(ct);

        // Garante que todos os tipos existam (contas criadas antes de um novo tipo surgir).
        var missing = Enum.GetValues<NotificationType>().Where(t => existing.All(p => p.Type != t)).ToList();
        if (missing.Count > 0)
        {
            foreach (var type in missing)
            {
                var pref = new NotificationPreference { UserId = UserId, Type = type };
                _db.NotificationPreferences.Add(pref);
                existing.Add(pref);
            }
            await _db.SaveChangesAsync(ct);
        }

        return existing.OrderBy(p => (int)p.Type).Select(Map).ToList();
    }

    public async Task<NotificationPreferenceDto> UpdateAsync(NotificationType type, UpdateNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(type))
            throw new AppValidationException(nameof(type), "Tipo de notificação inválido.");
        if (!string.IsNullOrWhiteSpace(request.PreferredTime) && !TimeOnly.TryParse(request.PreferredTime, out _))
            throw new AppValidationException(nameof(request.PreferredTime), "Horário inválido (use HH:mm).");

        var pref = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == UserId && p.Type == type, ct);
        if (pref is null)
        {
            pref = new NotificationPreference { UserId = UserId, Type = type };
            _db.NotificationPreferences.Add(pref);
        }
        pref.Enabled = request.Enabled;
        pref.HideSensitiveOnLockScreen = request.HideSensitiveOnLockScreen;
        pref.PreferredTime = string.IsNullOrWhiteSpace(request.PreferredTime) ? null : request.PreferredTime;
        pref.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(pref);
    }

    private static NotificationPreferenceDto Map(NotificationPreference p) => new()
    {
        Type = p.Type,
        TypeLabel = p.Type.GetDescription(),
        Enabled = p.Enabled,
        HideSensitiveOnLockScreen = p.HideSensitiveOnLockScreen,
        PreferredTime = p.PreferredTime
    };
}
