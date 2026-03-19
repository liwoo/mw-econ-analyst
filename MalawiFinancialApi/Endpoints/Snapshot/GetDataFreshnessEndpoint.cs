using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Snapshot;

public class GetDataFreshnessEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/data-freshness");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get data freshness status";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
