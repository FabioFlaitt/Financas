namespace FinanceApp.Mobile.Services;

/// <summary>
/// Configuração de endpoint da API. Ajuste <see cref="BaseUrl"/> para o endereço do backend.
/// - Simulador iOS: http://localhost:5219
/// - iPhone físico: use o IP da máquina na rede (ex.: http://192.168.0.10:5219) ou um domínio HTTPS.
/// Em produção use sempre HTTPS. Veja README para detalhes.
/// </summary>
public static class ApiConfig
{
    public static string BaseUrl { get; set; } = "http://localhost:5219";

    /// <summary>
    /// Quando verdadeiro, se a API estiver indisponível o app exibe dados de demonstração
    /// (claramente identificados) para que a interface nunca fique vazia no primeiro uso.
    /// </summary>
    public static bool FallbackToDemoData { get; set; } = true;
}
