using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
