namespace PlanToSave.Application.Flows;

/// <summary>User-defined column mapping from CSV headers to recognized fields.</summary>
public class CsvColumnMapping
{
    public string? DateColumn { get; set; }
    /// <summary>A single signed amount column (positive = in, negative = out).</summary>
    public string? AmountColumn { get; set; }
    /// <summary>Bank "credit" column — money flowing IN to the bank account.</summary>
    public string? CreditColumn { get; set; }
    /// <summary>Bank "debit" column — money flowing OUT of the bank account.</summary>
    public string? DebitColumn { get; set; }
    public string? DescriptionColumn { get; set; }
}

/// <summary>A single parsed row ready for the user to review.</summary>
/// <param name="Amount">Positive = money IN to bank account; negative = money OUT.</param>
public record CsvPreviewRow(
    int RowNumber,
    DateOnly? Date,
    decimal? Amount,
    string? Description,
    string? ParseError);

/// <summary>The full result of the initial CSV parse step.</summary>
public record CsvParseResult(
    List<string> DetectedHeaders,
    string DetectedDelimiter,
    CsvColumnMapping SuggestedMapping,
    List<CsvPreviewRow> PreviewRows,
    List<string> GlobalErrors);

/// <summary>A validated, direction-resolved row ready for bulk insert.</summary>
public record BulkImportRowDto(
    DateOnly Date,
    decimal Amount,
    string? Description,
    Guid FromAccountId,
    Guid ToAccountId);
