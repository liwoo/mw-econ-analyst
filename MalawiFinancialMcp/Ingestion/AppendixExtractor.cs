using MalawiFinancialMcp.Data.Models;
using Microsoft.Extensions.Logging;

namespace MalawiFinancialMcp.Ingestion;

public class AppendixExtractor
{
    private readonly ILogger<AppendixExtractor> _logger;

    private static readonly Dictionary<string, (string Name, string Category, string Unit)> IndicatorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MK/USD"] = ("mk_usd", "exchange_rate", "MWK"),
        ["MK/GBP"] = ("mk_gbp", "exchange_rate", "MWK"),
        ["MK/EUR"] = ("mk_eur", "exchange_rate", "MWK"),
        ["MK/ZAR"] = ("mk_zar", "exchange_rate", "MWK"),
        ["Gross Official Reserves"] = ("gross_reserves", "fx_reserves", "USD mn"),
        ["Headline CPI"] = ("headline_cpi", "inflation", "%"),
        ["Food CPI"] = ("food_cpi", "inflation", "%"),
        ["Non-Food CPI"] = ("nonfood_cpi", "inflation", "%"),
        ["Monetary Policy Rate"] = ("mpr", "interest_rate", "%"),
        ["MPR"] = ("mpr", "interest_rate", "%"),
        ["Overnight Interbank Rate"] = ("overnight_interbank", "interest_rate", "%"),
        ["Lombard Rate"] = ("lombard_rate", "interest_rate", "%"),
        ["Commercial Bank Reference Rate"] = ("commercial_reference_rate", "interest_rate", "%"),
        ["91-day TB"] = ("91_day_tb", "govt_securities", "%"),
        ["182-day TB"] = ("182_day_tb", "govt_securities", "%"),
        ["364-day TB"] = ("364_day_tb", "govt_securities", "%"),
        ["2-yr TN"] = ("2_yr_tn", "govt_securities", "%"),
        ["3-yr TN"] = ("3_yr_tn", "govt_securities", "%"),
        ["5-yr TN"] = ("5_yr_tn", "govt_securities", "%"),
        ["7-yr TN"] = ("7_yr_tn", "govt_securities", "%"),
        ["10-yr TN"] = ("10_yr_tn", "govt_securities", "%"),
        ["MASI YTD"] = ("masi_ytd", "equity_index", "%"),
        ["DSI YTD"] = ("dsi_ytd", "equity_index", "%"),
        ["FSI YTD"] = ("fsi_ytd", "equity_index", "%"),
    };

    private static readonly HashSet<string> StockTickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AIRTEL", "BHL", "FDHB", "FMBCH", "ICON", "ILLOVO", "MPICO",
        "NBM", "NBS", "NICO", "NITL", "OMU", "PCL", "STANDARD", "SUNBIRD", "TNM"
    };

    public AppendixExtractor(ILogger<AppendixExtractor> logger) => _logger = logger;

    public List<FinancialIndicator> Extract(DoclingDocument doc, DateOnly reportDate)
    {
        var indicators = new List<FinancialIndicator>();

        var tableItem = FindAppendixTable(doc);
        if (tableItem == null)
        {
            _logger.LogWarning("No appendix table found in document dated {Date}", reportDate);
            return indicators;
        }

        // Convert Docling's cell-based table to a simple row/col grid
        var grid = DoclingClient.TableToGrid(tableItem);
        if (grid.Count < 2)
        {
            _logger.LogWarning("Appendix table has too few rows ({Count})", grid.Count);
            return indicators;
        }

        // Parse column headers (row 0) as month-year dates
        var columnDates = ParseColumnDates(grid[0], reportDate);
        if (columnDates.Count == 0)
        {
            _logger.LogWarning("Could not parse column dates from appendix table");
            return indicators;
        }

        // Parse each data row
        for (var rowIdx = 1; rowIdx < grid.Count; rowIdx++)
        {
            var row = grid[rowIdx];
            if (row.Count == 0) continue;

            var rowHeader = row[0].Trim();
            if (string.IsNullOrWhiteSpace(rowHeader)) continue;

            var mapping = ResolveIndicator(rowHeader);
            if (mapping == null)
            {
                _logger.LogDebug("Unknown indicator row: {Header}", rowHeader);
                continue;
            }

            for (var colIdx = 0; colIdx < columnDates.Count && colIdx + 1 < row.Count; colIdx++)
            {
                var cellText = row[colIdx + 1]?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(cellText) || cellText == "-" || cellText == "N/A")
                    continue;

                if (TryParseNumeric(cellText, out var value))
                {
                    indicators.Add(new FinancialIndicator
                    {
                        ReportDate = columnDates[colIdx],
                        IndicatorName = mapping.Value.Name,
                        IndicatorValue = value,
                        Unit = mapping.Value.Unit,
                        Category = mapping.Value.Category,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} indicator values from appendix", indicators.Count);
        return indicators;
    }

    private static (string Name, string Category, string Unit)? ResolveIndicator(string rowHeader)
    {
        if (IndicatorMap.TryGetValue(rowHeader, out var map))
            return map;
        if (StockTickers.Contains(rowHeader))
            return (rowHeader.ToLowerInvariant(), "equity_price", "MWK");

        // Fuzzy match — strip whitespace and special chars
        var normalized = rowHeader.Replace(" ", "").Replace("-", "");
        foreach (var (key, value) in IndicatorMap)
        {
            if (key.Replace(" ", "").Replace("-", "").Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private DoclingTableItem? FindAppendixTable(DoclingDocument doc)
    {
        // The appendix table is the largest table (30+ rows, 10+ columns)
        return doc.Tables
            .Where(t => t.Data.NumRows >= 20 && t.Data.NumCols >= 8)
            .OrderByDescending(t => t.Data.NumRows * t.Data.NumCols)
            .FirstOrDefault();
    }

    private List<DateOnly> ParseColumnDates(List<string> headerRow, DateOnly reportDate)
    {
        var dates = new List<DateOnly>();
        foreach (var header in headerRow.Skip(1)) // skip row header column
        {
            if (TryParseMonthYear(header?.Trim() ?? "", out var date))
                dates.Add(date);
        }
        return dates;
    }

    private static bool TryParseMonthYear(string text, out DateOnly date)
    {
        date = default;
        var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4,
            ["may"] = 5, ["jun"] = 6, ["jul"] = 7, ["aug"] = 8,
            ["sep"] = 9, ["oct"] = 10, ["nov"] = 11, ["dec"] = 12,
            ["january"] = 1, ["february"] = 2, ["march"] = 3, ["april"] = 4,
            ["june"] = 6, ["july"] = 7, ["august"] = 8,
            ["september"] = 9, ["october"] = 10, ["november"] = 11, ["december"] = 12
        };

        var match = System.Text.RegularExpressions.Regex.Match(text, @"([A-Za-z]+)[\s\-]*(\d{2,4})");
        if (!match.Success) return false;
        if (!months.TryGetValue(match.Groups[1].Value, out var month)) return false;

        var yearStr = match.Groups[2].Value;
        var year = yearStr.Length == 2 ? 2000 + int.Parse(yearStr) : int.Parse(yearStr);

        date = new DateOnly(year, month, 1);
        return true;
    }

    private static bool TryParseNumeric(string text, out double value)
    {
        value = 0;
        text = text.Replace(",", "").Replace("%", "").Replace("bn", "").Trim();
        if (text.StartsWith('(') && text.EndsWith(')'))
            text = "-" + text[1..^1];
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
