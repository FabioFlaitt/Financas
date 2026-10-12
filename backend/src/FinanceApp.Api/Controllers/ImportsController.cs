using FinanceApp.Application.Imports;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

public class ImportsController : BaseApiController
{
    private readonly IStatementImportService _service;

    public ImportsController(IStatementImportService service) => _service = service;

    /// <summary>Analisa um extrato CSV/OFX e devolve os lançamentos encontrados, marcando duplicados.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ImportPreviewResult>> Preview(ImportPreviewRequest request, CancellationToken ct)
        => Ok(await _service.PreviewAsync(request, ct));

    /// <summary>Importa os lançamentos selecionados de forma idempotente (ignora os já existentes).</summary>
    [HttpPost("commit")]
    public async Task<ActionResult<ImportCommitResult>> Commit(ImportCommitRequest request, CancellationToken ct)
        => Ok(await _service.CommitAsync(request, ct));
}
