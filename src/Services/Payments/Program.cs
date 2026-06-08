using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Services.Payments;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=dcl;Username=dcl;Password=dcl";

builder.Services.AddPayments(connectionString);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq").Bind);
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payments").Bind);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
