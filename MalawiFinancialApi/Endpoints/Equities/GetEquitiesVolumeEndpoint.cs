using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Equities;

public class GetEquitiesVolumeEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/equities/volume");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get equities trading volume data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
