var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Entry point of the checkout saga. Implementation (persist Order + write OrderPlaced
// to the outbox in one transaction) lands with dotnet-dev — see spec §10, phase 2.
app.MapPost("/orders", () =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this assembly.
public partial class Program;
