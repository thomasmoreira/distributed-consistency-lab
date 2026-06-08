using Services.Payments;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<PaymentsConsumerHost>();

var host = builder.Build();
host.Run();
