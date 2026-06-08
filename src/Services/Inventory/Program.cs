using Services.Inventory;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<InventoryConsumerHost>();

var host = builder.Build();
host.Run();
