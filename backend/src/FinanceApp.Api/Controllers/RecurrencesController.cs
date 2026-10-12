using FinanceApp.Application.Recurrences;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class RecurrencesController : BaseApiController
{
    private readonly IRecurringTransactionService _service;

    public RecurrencesController(IRecurringTransactionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringTransactionDto>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<RecurringTransactionDto>> Create(SaveRecurringTransactionRequest request, CancellationToken ct)
        => Ok(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecurringTransactionDto>> Update(Guid id, SaveRecurringTransactionRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Gera os lançamentos das ocorrências vencidas até hoje.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<object>> Generate(CancellationToken ct)
        => Ok(new { generated = await _service.GenerateDueAsync(ct) });
}
