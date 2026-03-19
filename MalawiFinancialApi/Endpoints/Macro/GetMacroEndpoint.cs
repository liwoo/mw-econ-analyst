using FastEndpoints;

namespace MalawiFinancialApi.Endpoints.Macro;

public class GetMacroEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/macro");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get macroeconomic indicators";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // TODO: Implement
        HttpContext.Response.StatusCode = 200;
    }
}
