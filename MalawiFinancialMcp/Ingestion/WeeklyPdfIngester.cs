using MalawiFinancialMcp.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace MalawiFinancialMcp.Ingestion;

public class WeeklyPdfIngester
{
    private readonly DoclingClient _docling;
    private readonly AppendixExtractor _appendix;
    private readonly NarrativeExtractor _narrative;
    private readonly EntityTagger _tagger;
    private readonly AuctionExtractor _auction;
    private readonly IIndicatorRepository _indicatorRepo;
    private readonly IMarketEventRepository _eventRepo;
    private readonly IAuctionRepository _auctionRepo;
    private readonly ILogger<WeeklyPdfIngester> _logger;

    public WeeklyPdfIngester(
        DoclingClient docling,
        AppendixExtractor appendix,
        NarrativeExtractor narrative,
        EntityTagger tagger,
        AuctionExtractor auction,
        IIndicatorRepository indicatorRepo,
        IMarketEventRepository eventRepo,
        IAuctionRepository auctionRepo,
        ILogger<WeeklyPdfIngester> logger)
    {
        _docling = docling;
        _appendix = appendix;
        _narrative = narrative;
        _tagger = tagger;
        _auction = auction;
        _indicatorRepo = indicatorRepo;
        _eventRepo = eventRepo;
        _auctionRepo = auctionRepo;
        _logger = logger;
    }

    public async Task IngestAsync(PdfFileInfo file, CancellationToken ct = default)
    {
        _logger.LogInformation("Ingesting weekly PDF: {File} ({Date})",
            Path.GetFileName(file.FilePath), file.ReportDate);

        // 1. Parse PDF via Docling
        var doc = await _docling.ConvertAsync(file.FilePath, ct);

        // 2. Extract and persist appendix indicators (highest priority)
        var indicators = _appendix.Extract(doc, file.ReportDate);
        if (indicators.Count > 0)
        {
            await _indicatorRepo.BulkUpsertAsync(indicators);
            _logger.LogInformation("Persisted {Count} indicators", indicators.Count);
        }

        // 3. Extract narrative events and tag entities
        var events = _narrative.Extract(doc, file.ReportDate);
        foreach (var evt in events)
            evt.Entities = _tagger.Tag(evt.EventText);
        if (events.Count > 0)
        {
            await _eventRepo.BulkInsertAsync(events);
            _logger.LogInformation("Persisted {Count} market events", events.Count);
        }

        // 4. Extract auction data
        var auctions = _auction.Extract(doc, file.ReportDate);
        if (auctions.Count > 0)
        {
            await _auctionRepo.BulkUpsertAsync(auctions);
            _logger.LogInformation("Persisted {Count} auction rows", auctions.Count);
        }

        _logger.LogInformation("Completed ingestion of {File}", Path.GetFileName(file.FilePath));
    }
}
