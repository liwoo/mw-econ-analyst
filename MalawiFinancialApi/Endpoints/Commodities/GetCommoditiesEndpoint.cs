using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Commodities;

public class GetCommoditiesEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/commodities");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get commodities market data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
