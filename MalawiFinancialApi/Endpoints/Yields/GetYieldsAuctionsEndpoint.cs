using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Yields;

public class GetYieldsAuctionsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/yields/auctions");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get treasury auction results";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
