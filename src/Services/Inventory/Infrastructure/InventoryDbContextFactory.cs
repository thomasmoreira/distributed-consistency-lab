using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Services.Inventory.Infrastructure;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>. Not used at runtime.</summary>
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=dcl;Username=dcl;Password=dcl;SearchPath=inventory")
            .Options;

        return new InventoryDbContext(options);
    }
}
