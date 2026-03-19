using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Yields;

public class GetYieldsAuctionsTenorsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/yields/auctions/tenors");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get auction tenors breakdown";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
