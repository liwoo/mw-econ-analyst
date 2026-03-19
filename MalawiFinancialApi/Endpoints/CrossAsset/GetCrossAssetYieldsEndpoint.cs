using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.CrossAsset;

public class GetCrossAssetYieldsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/cross-asset/yields");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get cross-asset yield comparison";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
