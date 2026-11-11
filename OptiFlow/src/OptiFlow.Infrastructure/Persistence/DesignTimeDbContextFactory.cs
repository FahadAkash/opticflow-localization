using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OptiFlowDbContext>
{
    public OptiFlowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OptiFlowDbContext>();
        // Design-time placeholder used solely for EF Core migration codegen; never connects in offline mode.
        optionsBuilder.UseNpgsql("Host=localhost;Database=optiflow_design;Username=postgres");
        return new OptiFlowDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantId CurrentTenantId => TenantId.Empty;
        public string CurrentActorId => "design-time";
        public bool HasTenant => false;
        public void SetTenant(TenantId tenantId, string actorId) { }
    }
}
