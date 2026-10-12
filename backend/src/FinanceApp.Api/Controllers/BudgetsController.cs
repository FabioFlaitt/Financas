using FinanceApp.Application.Budgets;
using FinanceApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class BudgetsController : BaseApiController
{
    private readonly IBudgetService _service;
    private readonly IDateTimeProvider _clock;

    public BudgetsController(IBudgetService service, IDateTimeProvider clock)
    {
        _service = service;
        _clock = clock;
    }

    /// <summary>Resumo dos orçamentos do mês (padrão: mês atual) com consumo realizado.</summary>
    [HttpGet]
    public async Task<ActionResult<BudgetOverviewDto>> Get(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => Ok(await _service.GetOverviewAsync(year ?? _clock.Today.Year, month ?? _clock.Today.Month, ct));

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create(SaveBudgetRequest request, CancellationToken ct)
        => Ok(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BudgetDto>> Update(Guid id, SaveBudgetRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
