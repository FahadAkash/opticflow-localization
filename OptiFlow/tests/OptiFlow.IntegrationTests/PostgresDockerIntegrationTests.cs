using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace OptiFlow.IntegrationTests;

/// <summary>
/// Live PostgreSQL integration tests using Testcontainers.
/// Skipped when Docker is disabled via: dotnet test --filter "Category!=Docker"
/// </summary>
[Trait("Category", "Docker")]
public sealed class PostgresDockerIntegrationTests
{
    [Fact]
    public async Task MigrationsAndImmutabilityTrigger_InRealPostgreSql_EnforcesSnapshotImmutabilityAndSkipLocked()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        var tenantCtx = new TestTenantContext();
        var tenantId = TenantId.New();
        tenantCtx.SetTenant(tenantId, "pg-test");

        DbContextOptions<OptiFlowDbContext> options = new DbContextOptionsBuilder<OptiFlowDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new OptiFlowDbContext(options, tenantCtx);
        await db.Database.MigrateAsync();

        var itemId = ContentItemId.New();
        var versionId = ContentVersionId.New();
        await db.Tenants.AddAsync(new Tenant(tenantId, "PG Tenant", "pg-tenant", DateTimeOffset.UtcNow));
        await db.ContentItems.AddAsync(new ContentItem(
            itemId, tenantId, ConnectionId.New(), new ContentKey("pg-1"),
            "Page", new LocaleCode("en"), DateTimeOffset.UtcNow));
        await db.ContentVersions.AddAsync(new ContentVersion(
            versionId, tenantId, itemId, "v1", new LocaleCode("en"), 1,
            ContentHash.FromRawText("v1"), "{}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        // Immutability trigger should reject UPDATE on content_versions
        Func<Task> updateAttempt = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE content_versions SET external_version_key = 'hacked' WHERE id = {versionId.Value}");
        await updateAttempt.Should().ThrowAsync<Exception>();

        // SKIP LOCKED queue claim
        var jobId = JobId.New();
        await db.LocalizationJobs.AddAsync(new LocalizationJob(
            jobId, tenantId, itemId, versionId, null, new LocaleCode("es-ES"),
            new IdempotencyKey("pg-job-1"), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var queue = new PostgresJobQueue(db, TimeProvider.System);
        LocalizationJob? claimed = await queue.ClaimNextAsync("pg-worker-1", CancellationToken.None);
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(jobId);
        claimed.AttemptCount.Should().Be(1);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TenantId CurrentTenantId { get; private set; } = TenantId.Empty;
        public string CurrentActorId { get; private set; } = "test";
        public bool HasTenant => !CurrentTenantId.IsEmpty;
        public void SetTenant(TenantId tenantId, string actorId)
        {
            CurrentTenantId = tenantId;
            CurrentActorId = actorId;
        }
    }
}
