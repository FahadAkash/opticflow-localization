using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Persistence;

public sealed class OptiFlowDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenantContext;

    public OptiFlowDbContext(
        DbContextOptions<OptiFlowDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Evaluated dynamically per query by EF Core
    private TenantId CurrentTenantId => _tenantContext.CurrentTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<CmsConnection> CmsConnections => Set<CmsConnection>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<LocalizationJob> LocalizationJobs => Set<LocalizationJob>();
    public DbSet<LocalizationJobBlock> LocalizationJobBlocks => Set<LocalizationJobBlock>();
    public DbSet<TranslationMemoryEntry> TranslationMemoryEntries => Set<TranslationMemoryEntry>();
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();
    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();
    public DbSet<AiInvocation> AiInvocations => Set<AiInvocation>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<WebhookInbox> WebhookInbox => Set<WebhookInbox>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<ConnectionId>().HaveConversion<ConnectionIdConverter>();
        configurationBuilder.Properties<ContentItemId>().HaveConversion<ContentItemIdConverter>();
        configurationBuilder.Properties<ContentVersionId>().HaveConversion<ContentVersionIdConverter>();
        configurationBuilder.Properties<ContentBlockId>().HaveConversion<ContentBlockIdConverter>();
        configurationBuilder.Properties<TranslationId>().HaveConversion<TranslationIdConverter>();
        configurationBuilder.Properties<JobId>().HaveConversion<JobIdConverter>();
        configurationBuilder.Properties<JobBlockId>().HaveConversion<JobBlockIdConverter>();
        configurationBuilder.Properties<TranslationMemoryId>().HaveConversion<TranslationMemoryIdConverter>();
        configurationBuilder.Properties<GlossaryTermId>().HaveConversion<GlossaryTermIdConverter>();
        configurationBuilder.Properties<ReviewTaskId>().HaveConversion<ReviewTaskIdConverter>();
        configurationBuilder.Properties<AiInvocationId>().HaveConversion<AiInvocationIdConverter>();
        configurationBuilder.Properties<AuditEventId>().HaveConversion<AuditEventIdConverter>();
        configurationBuilder.Properties<WebhookInboxId>().HaveConversion<WebhookInboxIdConverter>();
        configurationBuilder.Properties<OutboxMessageId>().HaveConversion<OutboxMessageIdConverter>();
        configurationBuilder.Properties<ContentKey>().HaveConversion<ContentKeyConverter>();
        configurationBuilder.Properties<BlockKey>().HaveConversion<BlockKeyConverter>();
        configurationBuilder.Properties<LocaleCode>().HaveConversion<LocaleCodeConverter>();
        configurationBuilder.Properties<ContentHash>().HaveConversion<ContentHashConverter>();
        configurationBuilder.Properties<IdempotencyKey>().HaveConversion<IdempotencyKeyConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        bool isNpgsql = Database.IsNpgsql();
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new CmsConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new ContentItemConfiguration());
        modelBuilder.ApplyConfiguration(new ContentVersionConfiguration(isNpgsql));
        modelBuilder.ApplyConfiguration(new ContentBlockConfiguration());
        modelBuilder.ApplyConfiguration(new TranslationConfiguration());
        modelBuilder.ApplyConfiguration(new LocalizationJobConfiguration());
        modelBuilder.ApplyConfiguration(new LocalizationJobBlockConfiguration(isNpgsql));
        modelBuilder.ApplyConfiguration(new TranslationMemoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new GlossaryTermConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewTaskConfiguration());
        modelBuilder.ApplyConfiguration(new AiInvocationConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration(isNpgsql));
        modelBuilder.ApplyConfiguration(new WebhookInboxConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Fail-closed global tenant isolation filters
        modelBuilder.Entity<CmsConnection>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ContentItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ContentVersion>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ContentBlock>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Translation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LocalizationJob>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LocalizationJobBlock>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TranslationMemoryEntry>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<GlossaryTerm>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ReviewTask>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiInvocation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AuditEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<WebhookInbox>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        if (string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                    }
                }
            }
        }
    }

    private sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(v => v.Value, v => new TenantId(v));
    private sealed class ConnectionIdConverter() : ValueConverter<ConnectionId, Guid>(v => v.Value, v => new ConnectionId(v));
    private sealed class ContentItemIdConverter() : ValueConverter<ContentItemId, Guid>(v => v.Value, v => new ContentItemId(v));
    private sealed class ContentVersionIdConverter() : ValueConverter<ContentVersionId, Guid>(v => v.Value, v => new ContentVersionId(v));
    private sealed class ContentBlockIdConverter() : ValueConverter<ContentBlockId, Guid>(v => v.Value, v => new ContentBlockId(v));
    private sealed class TranslationIdConverter() : ValueConverter<TranslationId, Guid>(v => v.Value, v => new TranslationId(v));
    private sealed class JobIdConverter() : ValueConverter<JobId, Guid>(v => v.Value, v => new JobId(v));
    private sealed class JobBlockIdConverter() : ValueConverter<JobBlockId, Guid>(v => v.Value, v => new JobBlockId(v));
    private sealed class TranslationMemoryIdConverter() : ValueConverter<TranslationMemoryId, Guid>(v => v.Value, v => new TranslationMemoryId(v));
    private sealed class GlossaryTermIdConverter() : ValueConverter<GlossaryTermId, Guid>(v => v.Value, v => new GlossaryTermId(v));
    private sealed class ReviewTaskIdConverter() : ValueConverter<ReviewTaskId, Guid>(v => v.Value, v => new ReviewTaskId(v));
    private sealed class AiInvocationIdConverter() : ValueConverter<AiInvocationId, Guid>(v => v.Value, v => new AiInvocationId(v));
    private sealed class AuditEventIdConverter() : ValueConverter<AuditEventId, Guid>(v => v.Value, v => new AuditEventId(v));
    private sealed class WebhookInboxIdConverter() : ValueConverter<WebhookInboxId, Guid>(v => v.Value, v => new WebhookInboxId(v));
    private sealed class OutboxMessageIdConverter() : ValueConverter<OutboxMessageId, Guid>(v => v.Value, v => new OutboxMessageId(v));
    private sealed class ContentKeyConverter() : ValueConverter<ContentKey, string>(v => v.Value, v => new ContentKey(v));
    private sealed class BlockKeyConverter() : ValueConverter<BlockKey, string>(v => v.Value, v => new BlockKey(v));
    private sealed class LocaleCodeConverter() : ValueConverter<LocaleCode, string>(v => v.Value, v => new LocaleCode(v));
    private sealed class ContentHashConverter() : ValueConverter<ContentHash, string>(v => v.Value, v => new ContentHash(v));
    private sealed class IdempotencyKeyConverter() : ValueConverter<IdempotencyKey, string>(v => v.Value, v => new IdempotencyKey(v));
}
