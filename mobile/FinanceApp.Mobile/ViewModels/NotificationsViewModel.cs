using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>Uma preferência de notificação com o toggle que persiste a mudança na API.</summary>
public partial class PreferenceRow : ObservableObject
{
    private readonly Func<PreferenceRow, Task> _onChanged;

    public PreferenceRow(NotificationPreferenceItem item, Func<PreferenceRow, Task> onChanged)
    {
        Item = item;
        _enabled = item.Enabled;     // atribui o campo direto p/ não disparar o callback no carregamento
        _onChanged = onChanged;
    }

    public NotificationPreferenceItem Item { get; }
    public string TypeLabel => Item.TypeLabel;

    [ObservableProperty] private bool _enabled;

    partial void OnEnabledChanged(bool value) => _ = _onChanged(this);
}

public partial class NotificationsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private bool _persisting;

    public NotificationsViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<AlertItem> Alerts { get; } = new();
    public ObservableCollection<PreferenceRow> Preferences { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasAlerts;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var alerts = await _api.GetAlertsAsync();
            Alerts.Clear();
            foreach (var a in alerts) Alerts.Add(a);
            HasAlerts = Alerts.Count > 0;

            var prefs = await _api.GetNotificationPreferencesAsync();
            Preferences.Clear();
            foreach (var p in prefs) Preferences.Add(new PreferenceRow(p, PersistAsync));
        }
        catch { /* mantém o que já estava carregado */ }
        finally { IsBusy = false; }
    }

    private async Task PersistAsync(PreferenceRow row)
    {
        if (_persisting) return;
        _persisting = true;
        try
        {
            await _api.UpdateNotificationPreferenceAsync(row.Item.Type, new
            {
                enabled = row.Enabled,
                hideSensitiveOnLockScreen = row.Item.HideSensitiveOnLockScreen,
                preferredTime = row.Item.PreferredTime
            });
            row.Item.Enabled = row.Enabled;
        }
        catch
        {
            // Reverte o visual se a chamada falhar.
            row.Enabled = row.Item.Enabled;
        }
        finally { _persisting = false; }
    }
}
