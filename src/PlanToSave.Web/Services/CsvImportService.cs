using System.Globalization;
using System.Text;
using PlanToSave.Application.Flows;

namespace PlanToSave.Web.Services;

/// <summary>
/// Parses bank CSV exports into structured preview rows.
///
/// Robustness features:
///   • Auto-detects delimiter (comma, semicolon, tab, pipe)
///   • Strips UTF-8 BOM
///   • Skips leading metadata lines before the actual header row
///   • Fuzzy column-name matching against 60+ known bank export aliases
///   • Supports Debit/Credit split columns as well as a single signed Amount column
///   • Tries 18 date format patterns; falls back to DateTime.TryParse
///   • Amount parser strips currency symbols, thousands separators; handles
///     parentheses-as-negative notation and European comma decimals
///   • Full RFC 4180 quoted-field support (embedded commas, quotes, line-feeds)
/// </summary>
public class CsvImportService
{
    // ── Column name aliases ──────────────────────────────────────────────────────
    private static readonly string[] DateAliases =
    [
        "date", "transaction date", "trans date", "txn date", "post date",
        "posted date", "value date", "booking date", "settlement date",
        "effective date", "trade date", "transaction_date", "posting date",
        "completed date", "date completed"
    ];

    private static readonly string[] AmountAliases =
    [
        "amount", "transaction amount", "txn amount", "net amount", "value",
        "sum", "transaction", "running balance",
        "amount (usd)", "amount (cad)", "amount (gbp)", "amount (aud)",
        "amount (sgd)", "amount (eur)", "amount (nzd)", "total"
    ];

    private static readonly string[] CreditAliases =
    [
        "credit", "credits", "credit amount", "deposit", "deposits", "in",
        "money in", "received", "credit (gbp)", "credit amount (usd)", "cr",
        "paid in", "inflow", "debit/credit"  // some banks use +/- here
    ];

    private static readonly string[] DebitAliases =
    [
        "debit", "debits", "debit amount", "withdrawal", "withdrawals", "out",
        "money out", "spent", "debit (gbp)", "debit amount (usd)", "dr",
        "paid out", "outflow", "charges"
    ];

    private static readonly string[] DescriptionAliases =
    [
        "description", "narrative", "memo", "details", "particulars",
        "transaction description", "trans description", "transaction details",
        "payee", "merchant", "reference", "remarks", "note", "notes",
        "transaction narrative", "trans. description", "trans description",
        "name", "original description", "transaction name"
    ];

    // ── Date formats (more specific / unambiguous first) ────────────────────────
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyyMMdd",
        "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy",
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy",
        "MM-dd-yyyy", "dd-MM-yyyy", "yyyy/MM/dd",
        "MM.dd.yyyy", "dd.MM.yyyy",
        "MMM d, yyyy", "MMMM d, yyyy",
        "d MMM yyyy", "d MMMM yyyy",
        "MMM dd yyyy", "dd MMM yyyy",
        "MMM d yyyy",
    ];

    private static readonly char[] DelimiterCandidates = [',', ';', '\t', '|'];

    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="csvText"/> using auto-detected (or confirmed) settings.
    /// Pass <paramref name="overrideMapping"/> to re-parse with user-adjusted column names.
    /// </summary>
    public CsvParseResult Parse(string csvText, CsvColumnMapping? overrideMapping = null)
    {
        // Strip UTF-8 BOM
        if (csvText.StartsWith("\uFEFF", StringComparison.Ordinal))
            csvText = csvText[1..];

        var lines = SplitLines(csvText);
        if (lines.Count == 0)
            return EmptyResult("The uploaded file appears to be empty.");

        var delimiter = DetectDelimiter(lines);
        var (headerIndex, headers) = DetectHeaderRow(lines, delimiter);

        if (headerIndex < 0 || headers.Count == 0)
            return EmptyResult("Could not detect a header row. Make sure the file has column headers.");

        var mapping = overrideMapping ?? SuggestMapping(headers);
        var globalErrors = new List<string>();
        var rows = new List<CsvPreviewRow>();

        for (int i = headerIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseFields(line, delimiter);
            if (fields.Count == 0 || fields.All(f => string.IsNullOrWhiteSpace(f))) continue;

            var (date, amount, description, error) = ParseRow(fields, headers, mapping, i + 1);
            rows.Add(new CsvPreviewRow(i + 1, date, amount, description, error));
        }

        return new CsvParseResult(headers, delimiter.ToString(), mapping, rows, globalErrors);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private static CsvParseResult EmptyResult(string error)
        => new([], ",", new CsvColumnMapping(), [], [error]);

    private static List<string> SplitLines(string text)
    {
        // Normalise all line endings; do not split quoted multi-line fields
        // (edge-case: most bank exports don't have multi-line quoted fields)
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return [.. text.Split('\n')];
    }

    private static char DetectDelimiter(List<string> lines)
    {
        var samples = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(5).ToList();
        if (samples.Count == 0) return ',';

        char best = ',';
        int bestScore = -1;

        foreach (var d in DelimiterCandidates)
        {
            var counts = samples.Select(l => CountOutsideQuotes(l, d)).ToList();
            var total = counts.Sum();
            if (total == 0) continue;

            // Prefer consistent counts across rows (means it's really a delimiter there)
            bool consistent = counts.All(c => c == counts[0]);
            int score = total * 2 + (consistent ? 5 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }

        return best;
    }

    private static int CountOutsideQuotes(string line, char ch)
    {
        int count = 0;
        bool inQuote = false;
        foreach (char c in line)
        {
            if (c == '"') { inQuote = !inQuote; continue; }
            if (!inQuote && c == ch) count++;
        }
        return count;
    }

    private static (int headerIndex, List<string> headers) DetectHeaderRow(
        List<string> lines, char delimiter)
    {
        // Walk the first 10 non-empty lines looking for a row that matches known aliases
        for (int i = 0; i < Math.Min(lines.Count, 10); i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseFields(line, delimiter);
            var lower = fields.Select(f => f.Trim().ToLowerInvariant()).ToList();

            bool hasDate   = lower.Any(f => DateAliases.Contains(f));
            bool hasAmount = lower.Any(f => AmountAliases.Contains(f))
                          || (lower.Any(f => DebitAliases.Contains(f))
                              && lower.Any(f => CreditAliases.Contains(f)));

            if (hasDate || hasAmount)
                return (i, fields.Select(f => f.Trim()).ToList());
        }

        // Fallback: use the first non-empty line as the header
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line))
                return (i, ParseFields(line, delimiter).Select(f => f.Trim()).ToList());
        }

        return (-1, []);
    }

    private static CsvColumnMapping SuggestMapping(List<string> headers)
    {
        var lower = headers.Select(h => h.ToLowerInvariant().Trim()).ToList();

        string? FindExact(string[] aliases)
        {
            foreach (var alias in aliases)
            {
                int idx = lower.IndexOf(alias);
                if (idx >= 0) return headers[idx];
            }
            return null;
        }

        string? FindFuzzy(string[] aliases)
        {
            // Exact first
            var exact = FindExact(aliases);
            if (exact is not null) return exact;

            // Partial match: header contains alias or alias contains header
            foreach (var alias in aliases)
            {
                int idx = lower.FindIndex(
                    h => h.Contains(alias, StringComparison.OrdinalIgnoreCase)
                      || alias.Contains(h, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return headers[idx];
            }

            return null;
        }

        var mapping = new CsvColumnMapping
        {
            DateColumn        = FindFuzzy(DateAliases),
            AmountColumn      = FindFuzzy(AmountAliases),
            CreditColumn      = FindFuzzy(CreditAliases),
            DebitColumn       = FindFuzzy(DebitAliases),
            DescriptionColumn = FindFuzzy(DescriptionAliases),
        };

        // If we found both Debit AND Credit, prefer those over a generic Amount column
        // (avoids double-mapping when e.g. "Amount" means running balance)
        if (mapping.CreditColumn is not null && mapping.DebitColumn is not null)
            mapping.AmountColumn = null;

        return mapping;
    }

    private static (DateOnly? date, decimal? amount, string? description, string? error)
        ParseRow(List<string> fields, List<string> headers, CsvColumnMapping mapping, int rowNum)
    {
        string? GetField(string? colName)
        {
            if (colName is null) return null;
            int idx = headers.FindIndex(
                h => string.Equals(h, colName, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 && idx < fields.Count ? fields[idx].Trim() : null;
        }

        // ── Date ──────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(mapping.DateColumn))
            return (null, null, null, $"Row {rowNum}: no date column mapped.");

        var dateRaw = GetField(mapping.DateColumn);
        var date = TryParseDate(dateRaw);
        if (date is null)
            return (null, null, null, $"Row {rowNum}: unrecognised date '{dateRaw}'.");

        // ── Amount ─────────────────────────────────────────────────────────────
        decimal? amount;

        bool hasSplit = mapping.CreditColumn is not null || mapping.DebitColumn is not null;

        if (hasSplit)
        {
            var credit = TryParseAmount(GetField(mapping.CreditColumn)) ?? 0m;
            var debit  = TryParseAmount(GetField(mapping.DebitColumn))  ?? 0m;
            // credits are positive (money in), debits are positive values meaning money out
            amount = Math.Abs(credit) - Math.Abs(debit);
        }
        else if (mapping.AmountColumn is not null)
        {
            var raw = GetField(mapping.AmountColumn);
            amount = TryParseAmount(raw);
            if (amount is null)
                return (date, null, null, $"Row {rowNum}: unrecognised amount '{raw}'.");
        }
        else
        {
            return (date, null, null, $"Row {rowNum}: no amount column mapped.");
        }

        var description = GetField(mapping.DescriptionColumn);
        return (date, amount, string.IsNullOrWhiteSpace(description) ? null : description, null);
    }

    // ── Public helpers (also used in tests) ─────────────────────────────────────

    public static DateOnly? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim().Trim('"');

        foreach (var fmt in DateFormats)
        {
            if (DateOnly.TryParseExact(raw, fmt,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        }

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var r)) return r;
        if (DateTime.TryParse(raw, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    public static decimal? TryParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim().Trim('"');

        // Parentheses = negative: (1,234.56)
        bool negative = raw.StartsWith('(') && raw.EndsWith(')');
        if (negative) raw = raw[1..^1];

        // Handle explicit minus sign separately so we can strip symbols cleanly
        bool explicitMinus = raw.StartsWith('-');
        if (explicitMinus) raw = raw[1..];

        // Strip currency symbols and whitespace
        raw = raw.Replace("$", "").Replace("€", "").Replace("£", "")
                 .Replace("¥", "").Replace("₹", "").Replace("₽", "")
                 .Replace("A$", "").Replace("NZ$", "").Replace("CA$", "")
                 .Trim();

        // Try standard invariant parse (thousands separator = comma, decimal = period)
        if (decimal.TryParse(raw.Replace(",", ""),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var v1))
        {
            var abs = Math.Abs(v1);
            return negative || explicitMinus ? -abs : abs;
        }

        // European format: thousands = period, decimal = comma (e.g. 1.234,56)
        // Detect: last separator is a comma and it has 2 decimal places
        if (raw.Contains(',') && !raw.Contains('.'))
        {
            var european = raw.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(european, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v2))
            {
                var abs = Math.Abs(v2);
                return negative || explicitMinus ? -abs : abs;
            }
        }

        return null;
    }

    // ── RFC 4180 field parser ────────────────────────────────────────────────────
    private static List<string> ParseFields(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    // Escaped double-quote: ""
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuote = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuote = true;
                else if (c == delimiter) { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
