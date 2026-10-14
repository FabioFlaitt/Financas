using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Lembra o usuário de fazer backup e faz o export para a nuvem (iCloud/Drive) via folha de
/// compartilhamento. Guarda a data do último backup em <see cref="Preferences"/> (entra no backup do
/// aparelho). O app é local-first: o backup é um arquivo do banco que o usuário salva onde quiser.
/// </summary>
public class BackupReminderService
{
    private const string LastKey = "last_backup_utc";
    private const string SnoozeKey = "backup_snooze_utc";
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);
    private static readonly TimeSpan SnoozeFor = TimeSpan.FromDays(1);

    private readonly LocalFinanceStore _store;

    public BackupReminderService(LocalFinanceStore store) => _store = store;

    public DateTime? LastBackupUtc => Get(LastKey);

    /// <summary>true quando nunca houve backup ou já faz mais de 7 dias (e não está em soneca).</summary>
    public bool ShouldRemind()
    {
        if (Get(SnoozeKey) is DateTime snooze && DateTime.UtcNow < snooze) return false;
        var last = Get(LastKey);
        return last is null || DateTime.UtcNow - last.Value >= Interval;
    }

    public void SnoozeReminder() => Set(SnoozeKey, DateTime.UtcNow + SnoozeFor);

    public void MarkBackedUp() => Set(LastKey, DateTime.UtcNow);

    /// <summary>Gera o arquivo de backup e abre o compartilhamento (iCloud Drive/Google Drive/etc.).</summary>
    public async Task<bool> BackupNowAsync()
    {
        try
        {
            var file = await _store.CreateBackupFileAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Backup Flaitt Finanças",
                File = new ShareFile(file)
            });
            MarkBackedUp();
            return true;
        }
        catch { return false; }
    }

    private static DateTime? Get(string key)
    {
        try
        {
            var s = Preferences.Default.Get<string?>(key, null);
            return string.IsNullOrEmpty(s) ? null : DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch { return null; }
    }

    private static void Set(string key, DateTime value)
    {
        try { Preferences.Default.Set(key, value.ToString("O", CultureInfo.InvariantCulture)); } catch { }
    }
}
