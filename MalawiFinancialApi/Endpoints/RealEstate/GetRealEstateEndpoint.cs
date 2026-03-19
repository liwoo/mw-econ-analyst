using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.RealEstate;

public class GetRealEstateEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/real-estate");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get real estate market data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
