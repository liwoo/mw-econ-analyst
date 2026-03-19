using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Indices;

public class GetIndicesEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/indices");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get market indices data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
