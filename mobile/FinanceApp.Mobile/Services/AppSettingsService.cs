using Microsoft.Maui.Storage;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Guarda configurações sensíveis do app (hoje, a chave da API de rotas/pedágios) no armazenamento
/// seguro do aparelho. Fica fora do banco local para não entrar em backups e exports de dados.
/// </summary>
public class AppSettingsService
{
    private const string TollGuruKeyName = "tollguru_api_key";

    public async Task<string?> GetTollGuruApiKeyAsync()
    {
        try { return await SecureStorage.Default.GetAsync(TollGuruKeyName); }
        catch { return null; }
    }

    public async Task SetTollGuruApiKeyAsync(string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                SecureStorage.Default.Remove(TollGuruKeyName);
            else
                await SecureStorage.Default.SetAsync(TollGuruKeyName, value.Trim());
        }
        catch { /* SecureStorage pode falhar em alguns ambientes; ignora silenciosamente */ }
    }
}
