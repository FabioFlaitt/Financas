using FinanceApp.Application.Banking;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

/// <summary>
/// Endpoints de integração bancária (Open Finance). Nesta fase usa o provedor demonstrativo.
/// A troca por um provedor real não altera as regras financeiras — ver docs/open-finance-provedores.md.
/// </summary>
public class BankingController : BaseApiController
{
    private readonly IBankIntegrationProvider _provider;

    public BankingController(IBankIntegrationProvider provider) => _provider = provider;

    /// <summary>Informa qual provedor está ativo e se é modo demonstração.</summary>
    [HttpGet("provider")]
    public ActionResult<object> Provider()
        => Ok(new { name = _provider.Name, isDemo = _provider.IsDemo });

    /// <summary>Lista as instituições disponíveis para conexão no provedor atual.</summary>
    [HttpGet("institutions")]
    public async Task<ActionResult<IReadOnlyList<BankInstitution>>> Institutions(CancellationToken ct)
        => Ok(await _provider.GetInstitutionsAsync(ct));
}
