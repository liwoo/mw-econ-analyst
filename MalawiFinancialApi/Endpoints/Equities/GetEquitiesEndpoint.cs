using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Equities;

public class GetEquitiesEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/equities");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get equities market data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
