namespace FinanceApp.Infrastructure.Security;

/// <summary>Configuração do JWT, vinculada à seção "Jwt" do appsettings/variáveis de ambiente.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "FinanceApp";
    public string Audience { get; set; } = "FinanceApp.Mobile";

    /// <summary>Chave simétrica de assinatura (HS256). Deve vir de segredo/variável de ambiente.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Validade do token de acesso em minutos.</summary>
    public int AccessTokenMinutes { get; set; } = 60 * 12;
}
