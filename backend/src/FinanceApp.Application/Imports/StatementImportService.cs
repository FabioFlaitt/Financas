using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Imports;

public enum ImportFormat { Csv = 1, Ofx = 2 }

public record CsvOptions
{
    /// <summary>Separador; detectado automaticamente quando nulo (; , ou tab).</summary>
    public char? Delimiter { get; init; }
    public int DateColumn { get; init; } = 0;
    public int DescriptionColumn { get; init; } = 1;
    public int AmountColumn { get; init; } = 2;
    /// <summary>Formato da data (ex.: "dd/MM/yyyy"); tenta formatos comuns quando nulo.</summary>
    public string? DateFormat { get; init; }
    public bool HasHeader { get; init; } = true;
}

public record ImportItemDto
{
    public string ExternalId { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    /// <summary>Valor sempre positivo; o sinal está em <see cref="Type"/>.</summary>
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    /// <summary>Verdadeiro quando já existe um lançamento com este id externo (na prévia).</summary>
    public bool IsDuplicate { get; init; }
}

public record ImportPreviewRequest
{
    public Guid AccountId { get; init; }
    public ImportFormat Format { get; init; }
    public string Content { get; init; } = string.Empty;
    public CsvOptions? Csv { get; init; }
}

public record ImportPreviewResult
{
    public int TotalParsed { get; init; }
    public int Duplicates { get; init; }
    public IReadOnlyList<ImportItemDto> Items { get; init; } = new List<ImportItemDto>();
}

public record ImportCommitRequest
{
    public Guid AccountId { get; init; }
    public IReadOnlyList<ImportItemDto> Items { get; init; } = new List<ImportItemDto>();
}

public record ImportCommitResult
{
    public int Imported { get; init; }
    public int SkippedDuplicates { get; init; }
}

public interface IStatementImportService
{
    Task<ImportPreviewResult> PreviewAsync(ImportPreviewRequest request, CancellationToken ct = default);
    Task<ImportCommitResult> CommitAsync(ImportCommitRequest request, CancellationToken ct = default);
}

/// <summary>
/// Importa extratos CSV/OFX. Cada item recebe um <c>ExternalId</c> estável (FITID do OFX ou hash dos
/// dados no CSV, sempre prefixado pela conta) que garante a conciliação idempotente: reimportar o mesmo
/// arquivo não duplica lançamentos, pois o par (usuário, ExternalId) é único.
/// </summary>
public class StatementImportService : IStatementImportService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public StatementImportService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<ImportPreviewResult> PreviewAsync(ImportPreviewRequest request, CancellationToken ct = default)
    {
        await EnsureAccountAsync(request.AccountId, ct);

        var parsed = Parse(request);
        var externalIds = parsed.Select(p => p.ExternalId).ToList();

        var existing = await _db.Transactions
            .Where(t => t.UserId == UserId && t.ExternalId != null && externalIds.Contains(t.ExternalId))
            .Select(t => t.ExternalId!)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var items = parsed.Select(p => p with { IsDuplicate = existingSet.Contains(p.ExternalId) }).ToList();

        return new ImportPreviewResult
        {
            TotalParsed = items.Count,
            Duplicates = items.Count(i => i.IsDuplicate),
            Items = items
        };
    }

    public async Task<ImportCommitResult> CommitAsync(ImportCommitRequest request, CancellationToken ct = default)
    {
        await EnsureAccountAsync(request.AccountId, ct);
        if (request.Items.Count == 0)
            return new ImportCommitResult { Imported = 0, SkippedDuplicates = 0 };

        var externalIds = request.Items.Select(i => i.ExternalId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var existing = (await _db.Transactions
            .Where(t => t.UserId == UserId && t.ExternalId != null && externalIds.Contains(t.ExternalId))
            .Select(t => t.ExternalId!)
            .ToListAsync(ct)).ToHashSet();

        var imported = 0;
        var skipped = 0;
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId) || !existing.Add(item.ExternalId))
            {
                skipped++; // já existe no banco ou repetido no próprio lote
                continue;
            }

            _db.Transactions.Add(new Transaction
            {
                UserId = UserId,
                Description = string.IsNullOrWhiteSpace(item.Description) ? "Importado" : item.Description.Trim(),
                Amount = Math.Abs(item.Amount),
                Type = item.Type,
                Status = TransactionStatus.Confirmed,
                TransactionDate = item.Date,
                AccountId = request.AccountId,
                Origin = TransactionOrigin.ImportedFile,
                ExternalId = item.ExternalId
            });
            imported++;
        }

        await _db.SaveChangesAsync(ct);
        return new ImportCommitResult { Imported = imported, SkippedDuplicates = skipped };
    }

    private async Task EnsureAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (!await _db.Accounts.AnyAsync(a => a.Id == accountId && a.UserId == UserId, ct))
            throw new AppValidationException(nameof(accountId), "Conta inválida.");
    }

    private static List<ImportItemDto> Parse(ImportPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new AppValidationException(nameof(request.Content), "O arquivo está vazio.");

        var raw = request.Format switch
        {
            ImportFormat.Ofx => ParseOfx(request.Content),
            ImportFormat.Csv => ParseCsv(request.Content, request.Csv ?? new CsvOptions()),
            _ => throw new AppValidationException(nameof(request.Format), "Formato não suportado.")
        };

        return raw.Select(r =>
        {
            var type = r.signedAmount < 0 ? TransactionType.Expense : TransactionType.Income;
            return new ImportItemDto
            {
                ExternalId = BuildExternalId(request.AccountId, request.Format, r.sourceId, r.date, r.signedAmount, r.description),
                Date = r.date,
                Description = r.description,
                Amount = Math.Abs(r.signedAmount),
                Type = type,
                TypeLabel = type == TransactionType.Expense ? "Despesa" : "Receita",
                IsDuplicate = false
            };
        }).ToList();
    }

    private static string BuildExternalId(Guid accountId, ImportFormat format, string? sourceId, DateOnly date, decimal amount, string description)
    {
        if (!string.IsNullOrWhiteSpace(sourceId))
            return $"{accountId:N}:ofx:{sourceId}";
        var basis = $"{date:yyyyMMdd}|{amount.ToString(CultureInfo.InvariantCulture)}|{description.Trim().ToLowerInvariant()}";
        return $"{accountId:N}:{format.ToString().ToLowerInvariant()}:{ShortHash(basis)}";
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---- OFX ----

    private static readonly Regex StmtTrnRegex = new("<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static List<(DateOnly date, decimal signedAmount, string description, string? sourceId)> ParseOfx(string content)
    {
        var result = new List<(DateOnly, decimal, string, string?)>();
        foreach (Match m in StmtTrnRegex.Matches(content))
        {
            var block = m.Groups[1].Value;
            var amountText = OfxTag(block, "TRNAMT");
            var dateText = OfxTag(block, "DTPOSTED");
            if (amountText is null || dateText is null) continue;
            if (!TryParseDecimal(amountText, out var amount)) continue;
            if (!TryParseOfxDate(dateText, out var date)) continue;

            var name = OfxTag(block, "NAME");
            var memo = OfxTag(block, "MEMO");
            var description = (string.IsNullOrWhiteSpace(name) ? memo : name)?.Trim() ?? "Lançamento";
            var fitid = OfxTag(block, "FITID");

            result.Add((date, amount, description, string.IsNullOrWhiteSpace(fitid) ? null : fitid));
        }
        if (result.Count == 0)
            throw new AppValidationException(nameof(ImportPreviewRequest.Content), "Nenhuma transação OFX encontrada no arquivo.");
        return result;
    }

    private static string? OfxTag(string block, string tag)
    {
        var m = Regex.Match(block, $"<{tag}>([^<\r\n]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static bool TryParseOfxDate(string text, out DateOnly date)
    {
        date = default;
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length < 8) return false;
        return DateOnly.TryParseExact(digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // ---- CSV ----

    private static List<(DateOnly date, decimal signedAmount, string description, string? sourceId)> ParseCsv(string content, CsvOptions options)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
            throw new AppValidationException(nameof(ImportPreviewRequest.Content), "Arquivo CSV vazio.");

        var delimiter = options.Delimiter ?? DetectDelimiter(lines[0]);
        var dataLines = options.HasHeader ? lines.Skip(1) : lines;

        var result = new List<(DateOnly, decimal, string, string?)>();
        foreach (var line in dataLines)
        {
            var cols = SplitCsv(line, delimiter);
            if (cols.Count <= Math.Max(options.DateColumn, Math.Max(options.DescriptionColumn, options.AmountColumn)))
                continue;

            if (!TryParseCsvDate(cols[options.DateColumn], options.DateFormat, out var date)) continue;
            if (!TryParseDecimal(cols[options.AmountColumn], out var amount)) continue;
            var description = cols[options.DescriptionColumn].Trim();
            result.Add((date, amount, string.IsNullOrWhiteSpace(description) ? "Importado" : description, null));
        }
        if (result.Count == 0)
            throw new AppValidationException(nameof(ImportPreviewRequest.Content), "Nenhuma linha válida encontrada no CSV.");
        return result;
    }

    private static char DetectDelimiter(string headerLine)
    {
        int semic = headerLine.Count(c => c == ';');
        int comma = headerLine.Count(c => c == ',');
        int tab = headerLine.Count(c => c == '\t');
        if (tab >= semic && tab >= comma && tab > 0) return '\t';
        if (semic >= comma) return ';';
        return ',';
    }

    private static List<string> SplitCsv(string line, char delimiter)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == delimiter && !inQuotes) { cols.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        cols.Add(sb.ToString());
        return cols;
    }

    private static readonly string[] DateFormats =
        { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yy", "yyyyMMdd" };

    private static bool TryParseCsvDate(string text, string? format, out DateOnly date)
    {
        text = text.Trim();
        if (!string.IsNullOrWhiteSpace(format) &&
            DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        return DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        var negative = false;
        if (t.StartsWith('(') && t.EndsWith(')')) { negative = true; t = t[1..^1]; }
        t = t.Replace("R$", "").Replace(" ", "").Trim();
        if (t.EndsWith('-')) { negative = true; t = t[..^1]; }

        var hasComma = t.Contains(',');
        var hasDot = t.Contains('.');
        if (hasComma && hasDot)
            t = t.Replace(".", "").Replace(',', '.');      // 1.234,56 -> 1234.56
        else if (hasComma)
            t = t.Replace(',', '.');                        // 1234,56 -> 1234.56

        if (!decimal.TryParse(t, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            return false;
        if (negative) value = -Math.Abs(value);
        return true;
    }
}
