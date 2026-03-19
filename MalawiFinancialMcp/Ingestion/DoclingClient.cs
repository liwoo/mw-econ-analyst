using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MalawiFinancialMcp.Ingestion;

public class DoclingClient
{
    private readonly HttpClient _http;
    private readonly ILogger<DoclingClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DoclingClient(HttpClient http, ILogger<DoclingClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<DoclingDocument> ConvertAsync(string filePath, CancellationToken ct = default)
    {
        var cacheFile = filePath + ".docling.json";
        if (File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile) > File.GetLastWriteTimeUtc(filePath))
        {
            _logger.LogDebug("Using cached Docling output for {File}", Path.GetFileName(filePath));
            var cached = await File.ReadAllTextAsync(cacheFile, ct);
            return JsonSerializer.Deserialize<DoclingDocument>(cached, JsonOptions)!;
        }

        _logger.LogInformation("Sending {File} to Docling for conversion", Path.GetFileName(filePath));

        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _http.PostAsync("/api/v1/convert", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        // Cache the result
        await File.WriteAllTextAsync(cacheFile, json, ct);

        return JsonSerializer.Deserialize<DoclingDocument>(json, JsonOptions)!;
    }
}

// Response models for Docling API
public class DoclingDocument
{
    public List<DoclingPage> Pages { get; set; } = [];
    public List<DoclingTable> Tables { get; set; } = [];
    public List<DoclingTextBlock> TextBlocks { get; set; } = [];

    [JsonPropertyName("md_content")]
    public string? MarkdownContent { get; set; }
}

public class DoclingPage
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class DoclingTable
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("num_rows")]
    public int NumRows { get; set; }

    [JsonPropertyName("num_cols")]
    public int NumCols { get; set; }

    // Each row is a list of cell texts
    public List<List<string>> Data { get; set; } = [];

    // Header row labels
    public List<string>? Headers { get; set; }
}

public class DoclingTextBlock
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
    public string Text { get; set; } = "";

    [JsonPropertyName("block_type")]
    public string? BlockType { get; set; }
}
