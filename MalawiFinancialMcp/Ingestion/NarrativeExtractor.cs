using MalawiFinancialMcp.Data.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MalawiFinancialMcp.Ingestion;

public class NarrativeExtractor
{
    private readonly ILogger<NarrativeExtractor> _logger;

    public NarrativeExtractor(ILogger<NarrativeExtractor> logger) => _logger = logger;

    public List<MarketEvent> Extract(DoclingDocument doc, DateOnly reportDate)
    {
        var events = new List<MarketEvent>();

        // Get text blocks from pages 1-2
        var narrativeBlocks = doc.TextBlocks
            .Where(b => b.PageNumber <= 2)
            .Select(b => b.Text.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 30) // filter short headers
            .ToList();

        foreach (var text in narrativeBlocks)
        {
            // Split on bullet points or numbered items if present
            var items = SplitIntoItems(text);
            foreach (var item in items)
            {
                if (item.Length < 20) continue; // too short to be meaningful
                events.Add(new MarketEvent
                {
                    ReportDate = reportDate,
                    EventText = item,
                    SourceType = "weekly",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _logger.LogInformation("Extracted {Count} narrative events", events.Count);
        return events;
    }

    private static List<string> SplitIntoItems(string text)
    {
        // Split on common bullet/list patterns
        var lines = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var items = new List<string>();
        var current = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Check if line starts a new bullet/item
            if (Regex.IsMatch(trimmed, @"^[\u2022\-\u25AA\u25E6\d+\.]\s"))
            {
                if (!string.IsNullOrWhiteSpace(current))
                    items.Add(current.Trim());
                current = Regex.Replace(trimmed, @"^[\u2022\-\u25AA\u25E6\d+\.]\s*", "");
            }
            else
            {
                current += " " + trimmed;
            }
        }
        if (!string.IsNullOrWhiteSpace(current))
            items.Add(current.Trim());

        // If no bullet points were found, return the whole text as one item
        if (items.Count == 0 && text.Length > 30)
            items.Add(text.Trim());

        return items;
    }
}
