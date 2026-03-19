using MalawiFinancialMcp.Data.Models;
using Microsoft.Extensions.Logging;

namespace MalawiFinancialMcp.Ingestion;

public class AppendixExtractor
{
    private readonly ILogger<AppendixExtractor> _logger;

    // Map of known row header variations to canonical indicator names
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

    // Stock tickers
    private static readonly HashSet<string> StockTickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AIRTEL", "BHL", "FDHB", "FMBCH", "ICON", "ILLOVO", "MPICO",
        "NBM", "NBS", "NICO", "NITL", "OMU", "PCL", "STANDARD", "SUNBIRD", "TNM"
    };

    public AppendixExtractor(ILogger<AppendixExtractor> logger) => _logger = logger;

    public List<FinancialIndicator> Extract(DoclingDocument doc, DateOnly reportDate)
    {
        var indicators = new List<FinancialIndicator>();

        // Find the appendix table — look for a table with many rows (30+) and 10+ columns
        var appendixTable = FindAppendixTable(doc);
        if (appendixTable == null)
        {
            _logger.LogWarning("No appendix table found in document dated {Date}", reportDate);
            return indicators;
        }

        // Parse column headers as month-year dates
        var columnDates = ParseColumnDates(appendixTable, reportDate);
        if (columnDates.Count == 0)
        {
            _logger.LogWarning("Could not parse column dates from appendix table");
            return indicators;
        }

        // Parse each row
        for (var rowIdx = 1; rowIdx < appendixTable.Data.Count; rowIdx++) // skip header row
        {
            var row = appendixTable.Data[rowIdx];
            if (row.Count == 0) continue;

            var rowHeader = row[0].Trim();
            if (string.IsNullOrWhiteSpace(rowHeader)) continue;

            // Check if it's a known indicator or a stock ticker
            (string name, string category, string unit)? mapping = null;

            if (IndicatorMap.TryGetValue(rowHeader, out var map))
                mapping = map;
            else if (StockTickers.Contains(rowHeader))
                mapping = (rowHeader.ToLowerInvariant(), "equity_price", "MWK");
            else
            {
                // Try fuzzy matching — strip whitespace and special chars
                var normalized = rowHeader.Replace(" ", "").Replace("-", "");
                foreach (var (key, value) in IndicatorMap)
                {
                    if (key.Replace(" ", "").Replace("-", "").Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        mapping = value;
                        break;
                    }
                }
            }

            if (mapping == null)
            {
                _logger.LogDebug("Unknown indicator row: {Header}", rowHeader);
                continue;
            }

            // Parse each value column
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
                        IndicatorName = mapping.Value.name,
                        IndicatorValue = value,
                        Unit = mapping.Value.unit,
                        Category = mapping.Value.category,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} indicator values from appendix", indicators.Count);
        return indicators;
    }

    private DoclingTable? FindAppendixTable(DoclingDocument doc)
    {
        // The appendix table is typically the largest table, with 30+ rows and 10+ columns
        return doc.Tables
            .Where(t => t.Data.Count >= 20 && (t.Data.FirstOrDefault()?.Count ?? 0) >= 8)
            .OrderByDescending(t => t.Data.Count * (t.Data.FirstOrDefault()?.Count ?? 0))
            .FirstOrDefault();
    }

    private List<DateOnly> ParseColumnDates(DoclingTable table, DateOnly reportDate)
    {
        // First row should be headers like "Jan-25", "Feb-25", etc.
        var dates = new List<DateOnly>();
        if (table.Data.Count == 0) return dates;

        var headerRow = table.Headers ?? table.Data[0];

        foreach (var header in headerRow.Skip(1)) // skip row header column
        {
            if (TryParseMonthYear(header?.Trim() ?? "", reportDate.Year, out var date))
                dates.Add(date);
        }

        return dates;
    }

    private static bool TryParseMonthYear(string text, int contextYear, out DateOnly date)
    {
        date = default;
        // Patterns: "Jan-25", "Jan 25", "Jan-2025", "January 2025"
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
        // Strip commas, %, trailing whitespace
        text = text.Replace(",", "").Replace("%", "").Replace("bn", "").Trim();
        // Handle parentheses as negative: (5.2) -> -5.2
        if (text.StartsWith('(') && text.EndsWith(')'))
            text = "-" + text[1..^1];
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
