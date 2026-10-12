using System.Text.Json;
using FinanceApp.Application.Common.Exceptions;

namespace FinanceApp.Api.Middleware;

/// <summary>
/// Converte exceções da aplicação em respostas HTTP padronizadas (ProblemDetails),
/// sem vazar detalhes internos nem dados sensíveis.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppValidationException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Erro de validação", ex.Message, ex.Errors);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "Não encontrado", ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "Regra de negócio", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("não autenticado"))
        {
            await WriteAsync(context, StatusCodes.Status401Unauthorized, "Não autenticado", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "Erro interno", "Ocorreu um erro inesperado. Tente novamente.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string title, string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var payload = new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            detail,
            errors
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
