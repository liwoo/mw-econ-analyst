using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Commodities;

public class GetCommoditiesMilestonesEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/commodities/milestones");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get commodities price milestones";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
