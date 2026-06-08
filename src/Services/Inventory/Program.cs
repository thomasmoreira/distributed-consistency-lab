using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Inventory.Consumers;
using Services.Inventory.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=inventory";

builder.Services.AddDbContext<InventoryDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

builder.Services.AddOutboxInbox();
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq").Bind);
builder.Services.AddOutboxDispatcher();

builder.Services.AddIntegrationEventConsumer<OrderPlaced, OrderPlacedConsumer>();
builder.Services.AddRabbitMqConsumer("inventory");

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.MigrateAsync();
    await InventorySeeder.SeedAsync(db);
}

host.Run();
