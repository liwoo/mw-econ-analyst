using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Snapshot;

public class GetSnapshotEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/snapshot");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get current market snapshot";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
