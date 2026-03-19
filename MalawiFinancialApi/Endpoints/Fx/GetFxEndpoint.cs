using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Fx;

public class GetFxEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/fx");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get foreign exchange rates";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
