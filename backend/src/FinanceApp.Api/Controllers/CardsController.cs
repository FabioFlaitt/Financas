using FinanceApp.Application.Cards;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class CardsController : BaseApiController
{
    private readonly ICardService _cards;
    private readonly IInvoiceService _invoices;

    public CardsController(ICardService cards, IInvoiceService invoices)
    {
        _cards = cards;
        _invoices = invoices;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CardDto>>> List(CancellationToken ct)
        => Ok(await _cards.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CardDto>> Create(SaveCardRequest request, CancellationToken ct)
        => Ok(await _cards.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CardDto>> Update(Guid id, SaveCardRequest request, CancellationToken ct)
        => Ok(await _cards.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _cards.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/invoices")]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItemDto>>> Invoices(Guid id, CancellationToken ct)
        => Ok(await _invoices.ListByCardAsync(id, ct));

    [HttpPost("{id:guid}/installments")]
    public async Task<IActionResult> AddInstallment(Guid id, AddInstallmentRequest request, CancellationToken ct)
    {
        await _invoices.AddInstallmentAsync(request with { CreditCardId = id }, ct);
        return NoContent();
    }
}
