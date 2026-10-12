using FinanceApp.Application.Common.Models;
using FinanceApp.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class TransactionsController : BaseApiController
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service) => _service = service;

    /// <summary>Lista lançamentos com filtros e paginação.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List([FromQuery] TransactionListQuery query, CancellationToken ct)
        => Ok(await _service.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, UpdateTransactionRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(request with { Id = id }, ct));

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<TransactionDto>> Duplicate(Guid id, CancellationToken ct)
        => Ok(await _service.DuplicateAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
