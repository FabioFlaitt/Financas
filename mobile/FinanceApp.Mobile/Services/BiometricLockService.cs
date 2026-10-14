using Microsoft.Maui.Storage;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Trava de abertura por biometria (Face ID no iOS, digital/rosto no Android), via Plugin.Fingerprint.
/// A preferência fica em <see cref="Preferences"/>. Só é possível ativar quando o aparelho tem biometria.
/// </summary>
public class BiometricLockService
{
    private const string EnabledKey = "biometric_lock_enabled";

    public bool IsEnabled
    {
        get { try { return Preferences.Default.Get(EnabledKey, false); } catch { return false; } }
        set { try { Preferences.Default.Set(EnabledKey, value); } catch { } }
    }

    /// <summary>O aparelho tem biometria configurada e disponível?</summary>
    public async Task<bool> IsAvailableAsync()
    {
        try { return await CrossFingerprint.Current.IsAvailableAsync(); }
        catch { return false; }
    }

    /// <summary>Mostra o prompt de biometria. true se autenticou.</summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            var config = new AuthenticationRequestConfiguration("Flaitt Finanças", "Confirme para abrir o app")
            {
                AllowAlternativeAuthentication = true // deixa cair para o PIN/senha do aparelho
            };
            var result = await CrossFingerprint.Current.AuthenticateAsync(config);
            return result.Authenticated;
        }
        catch { return false; }
    }
}
