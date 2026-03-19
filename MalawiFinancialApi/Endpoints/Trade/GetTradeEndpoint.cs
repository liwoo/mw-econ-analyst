using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Trade;

public class GetTradeEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/trade");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get trade balance data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
