using System.Text.Json;
using System.Text.RegularExpressions;

namespace MalawiFinancialMcp.Ingestion;

public class EntityTagger
{
    private static readonly HashSet<string> Tickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AIRTEL", "BHL", "FDHB", "FMBCH", "ICON", "ILLOVO", "MPICO",
        "NBM", "NBS", "NICO", "NITL", "OMU", "PCL", "STANDARD", "SUNBIRD", "TNM"
    };

    private static readonly HashSet<string> Institutions = new(StringComparer.OrdinalIgnoreCase)
    {
        "RBM", "Reserve Bank", "MSE", "Malawi Stock Exchange",
        "GoM", "Government of Malawi", "IMF", "World Bank",
        "EIU", "Oxford Economics", "IFPRI", "OPEC", "MERA", "ESCOM"
    };

    private static readonly HashSet<string> Indicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "CPI", "GDP", "MPR", "inflation", "exchange rate", "interest rate",
        "Treasury Bill", "Treasury Note", "MASI", "DSI", "FSI"
    };

    public string Tag(string text)
    {
        var tickers = Tickers.Where(t => Regex.IsMatch(text, $@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase)).ToList();
        var institutions = Institutions.Where(i => text.Contains(i, StringComparison.OrdinalIgnoreCase)).ToList();
        var indicators = Indicators.Where(i => text.Contains(i, StringComparison.OrdinalIgnoreCase)).ToList();

        var tags = new Dictionary<string, List<string>>();
        if (tickers.Count > 0) tags["tickers"] = tickers;
        if (institutions.Count > 0) tags["institutions"] = institutions;
        if (indicators.Count > 0) tags["indicators"] = indicators;

        return tags.Count > 0 ? JsonSerializer.Serialize(tags) : "{}";
    }
}
