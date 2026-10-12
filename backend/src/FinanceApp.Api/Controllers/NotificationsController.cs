using FinanceApp.Application.Notifications;
using FinanceApp.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class NotificationsController : BaseApiController
{
    private readonly INotificationPreferenceService _preferences;
    private readonly IAlertsService _alerts;

    public NotificationsController(INotificationPreferenceService preferences, IAlertsService alerts)
    {
        _preferences = preferences;
        _alerts = alerts;
    }

    /// <summary>Alertas acionáveis do momento (orçamentos, faturas e contas a vencer).</summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> Alerts(CancellationToken ct)
        => Ok(await _alerts.GetAsync(ct));

    [HttpGet("preferences")]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceDto>>> Preferences(CancellationToken ct)
        => Ok(await _preferences.ListAsync(ct));

    [HttpPut("preferences/{type}")]
    public async Task<ActionResult<NotificationPreferenceDto>> UpdatePreference(
        NotificationType type, UpdateNotificationPreferenceRequest request, CancellationToken ct)
        => Ok(await _preferences.UpdateAsync(type, request, ct));
}
