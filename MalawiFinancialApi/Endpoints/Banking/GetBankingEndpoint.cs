using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Banking;

public class GetBankingEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/banking");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get banking sector data";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
