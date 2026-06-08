using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Payments.Consumers;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=payments";

builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<PaymentsDbContext>());

builder.Services.AddOutboxInbox();
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq").Bind);
builder.Services.AddOutboxDispatcher();

builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payments").Bind);
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

builder.Services.AddIntegrationEventConsumer<StockReserved, StockReservedConsumer>();
builder.Services.AddRabbitMqConsumer("payments");

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
