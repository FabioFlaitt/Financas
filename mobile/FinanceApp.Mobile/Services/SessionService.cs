namespace FinanceApp.Mobile.Services;

/// <summary>
/// Guarda o token de acesso de forma segura (Keychain no iOS, via SecureStorage do MAUI).
/// Mantém também um cache em memória para acesso rápido durante a sessão.
/// </summary>
public class SessionService
{
    private const string TokenKey = "finance.access_token";
    private const string UserNameKey = "finance.user_name";

    public string? AccessToken { get; private set; }
    public string? UserName { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public async Task LoadAsync()
    {
        try
        {
            AccessToken = await SecureStorage.Default.GetAsync(TokenKey);
            UserName = await SecureStorage.Default.GetAsync(UserNameKey);
        }
        catch
        {
            // SecureStorage pode falhar em alguns ambientes (ex.: sem entitlements). Ignora com segurança.
            AccessToken = null;
        }
    }

    public async Task SignInAsync(string token, string userName)
    {
        AccessToken = token;
        UserName = userName;
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
            await SecureStorage.Default.SetAsync(UserNameKey, userName);
        }
        catch { /* não bloqueia o fluxo caso o armazenamento seguro não esteja disponível */ }
    }

    public void SignOut()
    {
        AccessToken = null;
        UserName = null;
        try { SecureStorage.Default.RemoveAll(); } catch { }
    }
}
