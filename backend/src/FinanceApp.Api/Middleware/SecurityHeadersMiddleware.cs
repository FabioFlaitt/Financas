namespace FinanceApp.Api.Middleware;

/// <summary>
/// Adiciona cabeçalhos de segurança às respostas. Mantém-se conservador (sem CSP restritiva) para
/// não quebrar o Swagger UI; a API é consumida por um app nativo e devolve JSON.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-XSS-Protection"] = "0"; // recomendação moderna: desabilitar o filtro legado
        await _next(context);
    }
}
