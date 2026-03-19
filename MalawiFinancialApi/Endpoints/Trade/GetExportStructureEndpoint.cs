using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Trade;

public class GetExportStructureEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/export-structure");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get export structure breakdown";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
