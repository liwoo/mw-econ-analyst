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

        // Get text items from pages 1-2 via provenance
        var narrativeTexts = doc.Texts
            .Where(t => IsOnEarlyPages(t) && t.Text.Trim().Length > 30)
            .Select(t => t.Text.Trim())
            .ToList();

        foreach (var text in narrativeTexts)
        {
            var items = SplitIntoItems(text);
            foreach (var item in items)
            {
                if (item.Length < 20) continue;
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

    private static bool IsOnEarlyPages(DoclingTextItem text)
    {
        if (text.Prov == null || text.Prov.Count == 0) return true;
        return text.Prov.Any(p => p.PageNo <= 2);
    }

    private static List<string> SplitIntoItems(string text)
    {
        var lines = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var items = new List<string>();
        var current = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^[\u2022\-\u25AA\u25E6]\s|^\d+\.\s"))
            {
                if (!string.IsNullOrWhiteSpace(current))
                    items.Add(current.Trim());
                current = Regex.Replace(trimmed, @"^[\u2022\-\u25AA\u25E6]\s*|^\d+\.\s*", "");
            }
            else
            {
                current += " " + trimmed;
            }
        }
        if (!string.IsNullOrWhiteSpace(current))
            items.Add(current.Trim());

        if (items.Count == 0 && text.Length > 30)
            items.Add(text.Trim());

        return items;
    }
}
