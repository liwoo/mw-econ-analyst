using Dapper;
using MalawiFinancialMcp.Data;
using MalawiFinancialMcp.Data.Repositories;
using MalawiFinancialMcp.Ingestion;

// Dapper: map snake_case SQL columns to PascalCase C# properties
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
var connectionString = builder.Configuration.GetConnectionString("TimescaleDB")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:TimescaleDB");
builder.Services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<IIndicatorRepository, IndicatorRepository>();
builder.Services.AddScoped<IMarketEventRepository, MarketEventRepository>();
builder.Services.AddScoped<IAuctionRepository, AuctionRepository>();
builder.Services.AddScoped<IValuationRepository, ValuationRepository>();
builder.Services.AddScoped<ICommodityRepository, CommodityRepository>();
builder.Services.AddScoped<IForecastRepository, ForecastRepository>();
builder.Services.AddScoped<IBankingRepository, BankingRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<ITobaccoRepository, TobaccoRepository>();
builder.Services.AddScoped<IRealEstateRepository, RealEstateRepository>();

// Docling HTTP client
var doclingUrl = builder.Configuration["Docling:BaseUrl"] ?? "http://localhost:8080";
builder.Services.AddHttpClient<DoclingClient>(c => c.BaseAddress = new Uri(doclingUrl));

// Extractors
builder.Services.AddTransient<AppendixExtractor>();
builder.Services.AddTransient<NarrativeExtractor>();
builder.Services.AddTransient<EntityTagger>();
builder.Services.AddTransient<AuctionExtractor>();

// Ingesters
builder.Services.AddTransient<WeeklyPdfIngester>();

// Check for CLI ingestion mode
if (args.Length > 0 && args[0] == "ingest")
{
    var host = builder.Build();
    await RunIngestionAsync(host.Services, args);
    return;
}

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();

// CLI ingestion handler
static async Task RunIngestionAsync(IServiceProvider services, string[] args)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Ingestion");

    var type = GetArgValue(args, "--type") ?? "weekly";
    var directory = GetArgValue(args, "--directory") ?? "./data";
    var file = GetArgValue(args, "--file");
    var backfill = args.Contains("--backfill");

    logger.LogInformation("Starting ingestion — type={Type}, directory={Dir}, backfill={Backfill}",
        type, directory, backfill);

    if (type == "weekly")
    {
        var ingester = scope.ServiceProvider.GetRequiredService<WeeklyPdfIngester>();

        List<PdfFileInfo> files;
        if (!string.IsNullOrEmpty(file))
        {
            files = [PdfDateParser.Parse(file)];
        }
        else
        {
            files = PdfDateParser.DiscoverAll(directory)
                .Where(f => f.Type == PdfType.Weekly)
                .ToList();
        }

        logger.LogInformation("Found {Count} weekly PDFs to process", files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var pdf = files[i];
            try
            {
                logger.LogInformation("[{Idx}/{Total}] {File} ({Date})",
                    i + 1, files.Count, Path.GetFileName(pdf.FilePath), pdf.ReportDate);
                await ingester.IngestAsync(pdf);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ingest {File}", Path.GetFileName(pdf.FilePath));
            }
        }

        logger.LogInformation("Ingestion complete — processed {Count} files", files.Count);
    }
    else
    {
        logger.LogWarning("Ingestion type '{Type}' not yet implemented", type);
    }
}

static string? GetArgValue(string[] args, string key)
{
    var idx = Array.IndexOf(args, key);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
