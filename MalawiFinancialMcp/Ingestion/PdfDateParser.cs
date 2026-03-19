using System.Text.RegularExpressions;

namespace MalawiFinancialMcp.Ingestion;

public enum PdfType { Weekly, Monthly, Annual, Unknown }

public record PdfFileInfo(string FilePath, DateOnly ReportDate, PdfType Type);

public static class PdfDateParser
{
    private static readonly string[] MonthNames =
        ["January","February","March","April","May","June",
         "July","August","September","October","November","December"];

    // Regex: find day(1-31) + optional ordinal + month name + year(4 digits)
    private static readonly Regex WeeklyDateRegex = new(
        @"(\d{1,2})(?:st|nd|rd|th)?[\s\-]*(January|February|March|April|May|June|July|August|September|October|November|December)[\s\-]*(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Regex: month name + year (for monthly reports without day)
    private static readonly Regex MonthlyDateRegex = new(
        @"(January|February|March|April|May|June|July|August|September|October|November|December)[\s\-]*(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Regex: just year (for annual reports)
    private static readonly Regex YearOnlyRegex = new(
        @"(\d{4})", RegexOptions.Compiled);

    public static PdfFileInfo Parse(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Strip trailing -1, -2 suffixes and trailing hyphens
        fileName = Regex.Replace(fileName, @"-\d*$", "");

        var type = ClassifyType(filePath);

        // Try weekly pattern first (has day)
        var match = WeeklyDateRegex.Match(fileName);
        if (match.Success)
        {
            var day = int.Parse(match.Groups[1].Value);
            var month = ParseMonth(match.Groups[2].Value);
            var year = int.Parse(match.Groups[3].Value);
            return new PdfFileInfo(filePath, new DateOnly(year, month, day), type);
        }

        // Try monthly pattern (month + year, no day)
        match = MonthlyDateRegex.Match(fileName);
        if (match.Success)
        {
            var month = ParseMonth(match.Groups[1].Value);
            var year = int.Parse(match.Groups[2].Value);
            return new PdfFileInfo(filePath, new DateOnly(year, month, 1), type);
        }

        // Try year-only (annual reports)
        match = YearOnlyRegex.Match(fileName);
        if (match.Success)
        {
            var year = int.Parse(match.Groups[1].Value);
            return new PdfFileInfo(filePath, new DateOnly(year, 1, 1), type == PdfType.Unknown ? PdfType.Annual : type);
        }

        // Fallback: parse from parent directory name ("{month} {year}")
        return ParseFromDirectory(filePath, type);
    }

    public static List<PdfFileInfo> DiscoverAll(string baseDirectory)
    {
        // Recursively find all .pdf files, parse each, sort by date
        // Skip non-Bridgepath files (e.g., MSEBond-MarketSymposium)
        // Return sorted list oldest-first
        var results = new List<PdfFileInfo>();
        foreach (var pdf in Directory.EnumerateFiles(baseDirectory, "*.pdf", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(pdf);
            // Skip non-Bridgepath files
            if (!name.Contains("Bridgepath", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("Brigepath", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("Financial-Market", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("FinancialMarket", StringComparison.OrdinalIgnoreCase))
                continue;

            try { results.Add(Parse(pdf)); }
            catch { /* skip unparseable files */ }
        }
        return results.OrderBy(r => r.ReportDate).ToList();
    }

    private static PdfType ClassifyType(string filePath)
    {
        // If path contains /weekly/ it's weekly
        if (filePath.Contains("/weekly/", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("\\weekly\\", StringComparison.OrdinalIgnoreCase))
            return PdfType.Weekly;

        var name = Path.GetFileName(filePath);
        if (name.Contains("Annual", StringComparison.OrdinalIgnoreCase))
            return PdfType.Annual;
        if (name.Contains("Economic", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Monthly", StringComparison.OrdinalIgnoreCase))
            return PdfType.Monthly;

        return PdfType.Unknown;
    }

    private static int ParseMonth(string monthName)
    {
        return Array.FindIndex(MonthNames, m => m.Equals(monthName, StringComparison.OrdinalIgnoreCase)) + 1;
    }

    private static PdfFileInfo ParseFromDirectory(string filePath, PdfType type)
    {
        // Parent directory should be "{month} {year}" e.g., "january 2026"
        var dirName = Path.GetFileName(Path.GetDirectoryName(filePath)?.TrimEnd('/', '\\') ?? "");
        // If we're in /weekly/, go up one more level
        if (dirName.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            dirName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)?.TrimEnd('/', '\\') ?? "") ?? "");

        var match = MonthlyDateRegex.Match(dirName);
        if (match.Success)
        {
            var month = ParseMonth(match.Groups[1].Value);
            var year = int.Parse(match.Groups[2].Value);
            return new PdfFileInfo(filePath, new DateOnly(year, month, 1), type);
        }

        // Last resort
        return new PdfFileInfo(filePath, DateOnly.MinValue, PdfType.Unknown);
    }
}
