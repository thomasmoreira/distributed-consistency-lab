using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Services.Inventory;
using Services.Inventory.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=inventory";

builder.Services.AddInventory(connectionString);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq").Bind);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.MigrateAsync();
    await InventorySeeder.SeedAsync(db);
}

host.Run();
