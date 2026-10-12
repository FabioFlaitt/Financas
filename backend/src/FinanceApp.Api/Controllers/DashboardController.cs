using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboard;
    private readonly IDateTimeProvider _clock;

    public DashboardController(IDashboardService dashboard, IDateTimeProvider clock)
    {
        _dashboard = dashboard;
        _clock = clock;
    }

    /// <summary>Indicadores da tela inicial para o mês informado (padrão: mês atual).</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResult>> Get(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? creditCardId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
    {
        var query = new DashboardQuery
        {
            Year = year ?? _clock.Today.Year,
            Month = month ?? _clock.Today.Month,
            AccountId = accountId,
            CreditCardId = creditCardId,
            CategoryId = categoryId
        };
        return Ok(await _dashboard.GetAsync(query, ct));
    }
}
