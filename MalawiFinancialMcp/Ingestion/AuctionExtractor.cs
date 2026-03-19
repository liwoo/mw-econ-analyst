using MalawiFinancialMcp.Data.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MalawiFinancialMcp.Ingestion;

public class AuctionExtractor
{
    private readonly ILogger<AuctionExtractor> _logger;

    private static readonly HashSet<string> KnownTenors = new(StringComparer.OrdinalIgnoreCase)
    {
        "91-day", "182-day", "364-day", "2-yr", "3-yr", "5-yr", "7-yr", "10-yr"
    };

    public AuctionExtractor(ILogger<AuctionExtractor> logger) => _logger = logger;

    public List<AuctionEvent> Extract(DoclingDocument doc, DateOnly reportDate)
    {
        var auctions = new List<AuctionEvent>();

        var tableItem = FindAuctionTable(doc);
        if (tableItem == null)
        {
            _logger.LogWarning("No auction table found for {Date}", reportDate);
            return auctions;
        }

        var grid = DoclingClient.TableToGrid(tableItem);
        if (grid.Count < 2) return auctions;

        // Pre-compute total applied across all tenor rows
        var totalApplied = 0.0;
        for (var i = 1; i < grid.Count; i++)
        {
            if (grid[i].Count > 2 && TryParseDouble(grid[i][2], out var a))
                totalApplied += a;
        }

        for (var i = 1; i < grid.Count; i++)
        {
            var row = grid[i];
            if (row.Count < 2) continue;

            var tenor = NormalizeTenor(row[0]?.Trim() ?? "");
            if (tenor == null) continue;

            var auction = new AuctionEvent
            {
                ReportDate = reportDate,
                Tenor = tenor,
                CreatedAt = DateTime.UtcNow
            };

            if (row.Count > 1 && TryParseDouble(row[1], out var offered)) auction.Offered = offered;
            if (row.Count > 2 && TryParseDouble(row[2], out var applied)) auction.Applied = applied;
            if (row.Count > 3 && TryParseDouble(row[3], out var allotted)) auction.Allotted = allotted;
            if (row.Count > 4 && TryParseDouble(row[4], out var yield)) auction.Yield = yield;

            if (totalApplied > 0 && auction.Applied.HasValue)
                auction.PctOfTotalApplications = auction.Applied.Value / totalApplied * 100;

            auctions.Add(auction);
        }

        _logger.LogInformation("Extracted {Count} auction tenor rows", auctions.Count);
        return auctions;
    }

    private DoclingTableItem? FindAuctionTable(DoclingDocument doc)
    {
        // Look for a table with auction-related content (91-day, Treasury, Offered)
        return doc.Tables
            .Where(t => t.Data.TableCells.Any(cell =>
                cell.Text.Contains("91", StringComparison.OrdinalIgnoreCase) ||
                cell.Text.Contains("Treasury", StringComparison.OrdinalIgnoreCase) ||
                cell.Text.Contains("Offered", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Data.NumRows) // prefer smaller table (not the appendix)
            .FirstOrDefault();
    }

    private static string? NormalizeTenor(string text)
    {
        text = text.ToLowerInvariant();
        foreach (var t in KnownTenors)
        {
            if (text.Contains(t, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        var match = Regex.Match(text, @"(\d+)\s*-?\s*day", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var days = match.Groups[1].Value;
            if (days is "91" or "182" or "364")
                return $"{days}-day";
        }

        match = Regex.Match(text, @"(\d+)\s*-?\s*yr", RegexOptions.IgnoreCase);
        if (match.Success)
            return $"{match.Groups[1].Value}-yr";

        return null;
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Replace(",", "").Replace("%", "").Trim();
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
