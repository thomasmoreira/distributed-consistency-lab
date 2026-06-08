using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Features;
using Services.Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=orders";

// One scoped context instance serves both the domain (OrdersDbContext) and the outbox
// (MessagingDbContext) so they share a unit of work.
builder.Services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

builder.Services.AddOutboxInbox();
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq").Bind);
builder.Services.AddOutboxDispatcher();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PlaceOrderHandler>();

var app = builder.Build();

// Apply migrations on startup (lab convenience; a real deploy would run them separately).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", async (PlaceOrderRequest request, PlaceOrderHandler handler, CancellationToken ct) =>
{
    var response = await handler.HandleAsync(request, ct);
    return Results.Created($"/orders/{response.OrderId}", response);
});

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this assembly.
public partial class Program;
