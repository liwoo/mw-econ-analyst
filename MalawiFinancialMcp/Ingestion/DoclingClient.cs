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
        content.Add(fileContent, "files", Path.GetFileName(filePath));

        var response = await _http.PostAsync("/v1/convert/file", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var convertResponse = JsonSerializer.Deserialize<ConvertDocumentResponse>(json, JsonOptions)!;

        if (convertResponse.Document?.JsonContent == null)
            throw new InvalidOperationException(
                $"Docling returned no json_content for {Path.GetFileName(filePath)}. " +
                $"Status: {convertResponse.Status}");

        var doc = convertResponse.Document.JsonContent;

        // Cache the DoclingDocument (not the full response) for faster re-reads
        var docJson = JsonSerializer.Serialize(doc, JsonOptions);
        await File.WriteAllTextAsync(cacheFile, docJson, ct);

        _logger.LogInformation("Docling converted {File} in {Time:F1}s — {Tables} tables, {Texts} texts",
            Path.GetFileName(filePath), convertResponse.ProcessingTime,
            doc.Tables.Count, doc.Texts.Count);

        return doc;
    }

    /// <summary>
    /// Convert a DoclingDocument's TableItem into a simple row-based grid (list of string lists)
    /// for easy consumption by extractors.
    /// </summary>
    public static List<List<string>> TableToGrid(DoclingTableItem table)
    {
        var numRows = table.Data.NumRows;
        var numCols = table.Data.NumCols;
        var grid = new List<List<string>>(numRows);

        for (var r = 0; r < numRows; r++)
        {
            var row = new List<string>(numCols);
            for (var c = 0; c < numCols; c++)
                row.Add("");
            grid.Add(row);
        }

        foreach (var cell in table.Data.TableCells)
        {
            for (var r = cell.StartRowOffsetIdx; r < cell.EndRowOffsetIdx && r < numRows; r++)
            for (var c = cell.StartColOffsetIdx; c < cell.EndColOffsetIdx && c < numCols; c++)
                grid[r][c] = cell.Text;
        }

        return grid;
    }
}

// ── Docling-serve response envelope ──────────────────────────────────────────

public class ConvertDocumentResponse
{
    public ExportDocumentResponse? Document { get; set; }
    public string? Status { get; set; }
    public List<ErrorItem>? Errors { get; set; }

    [JsonPropertyName("processing_time")]
    public double ProcessingTime { get; set; }
}

public class ErrorItem
{
    public string? Message { get; set; }
}

public class ExportDocumentResponse
{
    public string? Filename { get; set; }

    [JsonPropertyName("md_content")]
    public string? MdContent { get; set; }

    [JsonPropertyName("json_content")]
    public DoclingDocument? JsonContent { get; set; }

    [JsonPropertyName("html_content")]
    public string? HtmlContent { get; set; }

    [JsonPropertyName("text_content")]
    public string? TextContent { get; set; }
}

// ── DoclingDocument (from docling-core) ──────────────────────────────────────

public class DoclingDocument
{
    [JsonPropertyName("schema_name")]
    public string? SchemaName { get; set; }

    public string? Version { get; set; }
    public string? Name { get; set; }

    public List<DoclingTextItem> Texts { get; set; } = [];
    public List<DoclingTableItem> Tables { get; set; } = [];
    public List<DoclingPictureItem> Pictures { get; set; } = [];
    public Dictionary<string, DoclingPageItem> Pages { get; set; } = new();

    public DoclingGroupItem? Body { get; set; }
    public DoclingGroupItem? Furniture { get; set; }
}

// ── Content items ────────────────────────────────────────────────────────────

public class DoclingTextItem
{
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public string Orig { get; set; } = "";

    [JsonPropertyName("self_ref")]
    public string? SelfRef { get; set; }

    public List<DoclingProvenance>? Prov { get; set; }
}

public class DoclingTableItem
{
    public string Label { get; set; } = "";

    [JsonPropertyName("self_ref")]
    public string? SelfRef { get; set; }

    public DoclingTableData Data { get; set; } = new();
    public List<DoclingProvenance>? Prov { get; set; }
}

public class DoclingTableData
{
    [JsonPropertyName("table_cells")]
    public List<DoclingTableCell> TableCells { get; set; } = [];

    [JsonPropertyName("num_rows")]
    public int NumRows { get; set; }

    [JsonPropertyName("num_cols")]
    public int NumCols { get; set; }
}

public class DoclingTableCell
{
    public string Text { get; set; } = "";

    [JsonPropertyName("row_span")]
    public int RowSpan { get; set; } = 1;

    [JsonPropertyName("col_span")]
    public int ColSpan { get; set; } = 1;

    [JsonPropertyName("start_row_offset_idx")]
    public int StartRowOffsetIdx { get; set; }

    [JsonPropertyName("end_row_offset_idx")]
    public int EndRowOffsetIdx { get; set; }

    [JsonPropertyName("start_col_offset_idx")]
    public int StartColOffsetIdx { get; set; }

    [JsonPropertyName("end_col_offset_idx")]
    public int EndColOffsetIdx { get; set; }

    [JsonPropertyName("column_header")]
    public bool ColumnHeader { get; set; }

    [JsonPropertyName("row_header")]
    public bool RowHeader { get; set; }

    [JsonPropertyName("row_section")]
    public bool RowSection { get; set; }
}

public class DoclingPictureItem
{
    public string Label { get; set; } = "";

    [JsonPropertyName("self_ref")]
    public string? SelfRef { get; set; }

    public List<DoclingProvenance>? Prov { get; set; }
}

// ── Structure & layout ───────────────────────────────────────────────────────

public class DoclingGroupItem
{
    public string? Name { get; set; }

    [JsonPropertyName("self_ref")]
    public string? SelfRef { get; set; }

    public List<DoclingChildRef> Children { get; set; } = [];
}

public class DoclingChildRef
{
    [JsonPropertyName("$ref")]
    public string? Ref { get; set; }
}

public class DoclingPageItem
{
    [JsonPropertyName("page_no")]
    public int PageNo { get; set; }

    public DoclingSize? Size { get; set; }
}

public class DoclingSize
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public class DoclingProvenance
{
    [JsonPropertyName("page_no")]
    public int PageNo { get; set; }

    public DoclingBoundingBox? Bbox { get; set; }
}

public class DoclingBoundingBox
{
    public double L { get; set; }
    public double T { get; set; }
    public double R { get; set; }
    public double B { get; set; }
}
