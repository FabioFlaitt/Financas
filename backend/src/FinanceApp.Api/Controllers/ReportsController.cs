using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class ReportsController : BaseApiController
{
    private readonly IReportService _service;
    private readonly IDateTimeProvider _clock;

    public ReportsController(IReportService service, IDateTimeProvider clock)
    {
        _service = service;
        _clock = clock;
    }

    /// <summary>
    /// Relatório consolidado: série mensal de receitas x despesas (últimos <paramref name="months"/> meses),
    /// despesas por categoria e fixas x variáveis para o mês informado (padrão: mês atual).
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> Summary(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] int months = 6, CancellationToken ct = default)
        => Ok(await _service.GetSummaryAsync(year ?? _clock.Today.Year, month ?? _clock.Today.Month, months, ct));
}
