using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Commodities;

public class GetCommoditiesTobaccoEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/commodities/tobacco");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get tobacco market data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
