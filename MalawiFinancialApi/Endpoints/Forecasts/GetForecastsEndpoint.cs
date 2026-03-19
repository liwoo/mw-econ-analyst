using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Forecasts;

public class GetForecastsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/forecasts");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get economic forecasts";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
