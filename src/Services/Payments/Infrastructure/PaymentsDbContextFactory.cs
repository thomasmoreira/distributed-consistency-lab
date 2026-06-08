using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Services.Payments.Infrastructure;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>. Not used at runtime.</summary>
public sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql("Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=payments")
            .Options;

        return new PaymentsDbContext(options);
    }
}
