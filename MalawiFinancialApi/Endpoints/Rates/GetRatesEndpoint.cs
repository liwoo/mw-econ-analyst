using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Rates;

public class GetRatesEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/rates");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get interest rates data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
