using Dapper;
using FastEndpoints;
using FastEndpoints.Swagger;
using MalawiFinancialMcp.Data;
using MalawiFinancialMcp.Data.Repositories;

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

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "Malawi Financial Intelligence API";
        s.Version = "v1";
        s.Description =
            "REST API for the Malawi Financial Intelligence platform. " +
            "20 endpoints covering equities, fixed income, FX, macro, commodities, " +
            "tobacco intelligence, real estate yields, and cross-asset yield matrix.";
    };
    o.ShortSchemaNames = true;
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
});
app.UseSwaggerGen();

app.Run();
