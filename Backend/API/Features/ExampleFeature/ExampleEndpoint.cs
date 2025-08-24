using FastEndpoints;

namespace API.Features.ExampleFeature;

public class ExampleEndpoint : Endpoint<ExampleRequest, ExampleResponse>
{
    public override void Configure()
    {
        Post("/example");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExampleRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ExampleResponse
        {
            FullName = $"{req.FirstName} {req.LastName}",
            IsOver18 = req.Age > 18,
        }, ct);
    }
}