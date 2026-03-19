using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Yields;

public class GetYieldsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/yields");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get yield curve data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
