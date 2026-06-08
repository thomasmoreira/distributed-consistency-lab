using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Services.Orders.Infrastructure;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without
/// spinning the web host. The connection string here is never used at runtime.
/// </summary>
public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql("Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=orders")
            .Options;

        return new OrdersDbContext(options);
    }
}
