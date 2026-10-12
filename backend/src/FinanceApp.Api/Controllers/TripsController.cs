using FinanceApp.Application.Trips;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

/// <summary>Viagens de carro: distância, pedágios, combustível, custo e rota (GPS).</summary>
public class TripsController : BaseApiController
{
    private readonly ITripService _service;

    public TripsController(ITripService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripDto>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TripDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    /// <summary>Inicia uma nova viagem.</summary>
    [HttpPost]
    public async Task<ActionResult<TripDetailDto>> Start(StartTripRequest request, CancellationToken ct)
        => Ok(await _service.StartAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TripDetailDto>> Update(Guid id, UpdateTripRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Encerra a viagem (opcionalmente com o odômetro final).</summary>
    [HttpPost("{id:guid}/finish")]
    public async Task<ActionResult<TripDetailDto>> Finish(Guid id, FinishTripRequest request, CancellationToken ct)
        => Ok(await _service.FinishAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TripDetailDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Registra um gasto (pedágio, combustível, etc.) na viagem.</summary>
    [HttpPost("{id:guid}/expenses")]
    public async Task<ActionResult<TripExpenseDto>> AddExpense(Guid id, AddTripExpenseRequest request, CancellationToken ct)
        => Ok(await _service.AddExpenseAsync(id, request, ct));

    [HttpDelete("{id:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, Guid expenseId, CancellationToken ct)
    {
        await _service.DeleteExpenseAsync(id, expenseId, ct);
        return NoContent();
    }

    /// <summary>Envia um ponto de GPS; o servidor soma a distância percorrida e devolve o resumo.</summary>
    [HttpPost("{id:guid}/locations")]
    public async Task<ActionResult<TripSummary>> AddLocation(Guid id, AddLocationPointRequest request, CancellationToken ct)
        => Ok(await _service.AddLocationPointAsync(id, request, ct));
}
