using FinanceApp.Application.Cards;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class InvoicesController : BaseApiController
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices) => _invoices = invoices;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _invoices.GetAsync(id, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<InvoiceListItemDto>> Close(Guid id, CancellationToken ct)
        => Ok(await _invoices.CloseAsync(id, ct));

    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<InvoiceListItemDto>> Pay(Guid id, PayInvoiceRequest request, CancellationToken ct)
        => Ok(await _invoices.PayAsync(id, request, ct));
}
