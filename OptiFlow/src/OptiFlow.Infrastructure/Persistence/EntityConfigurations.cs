using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiFlow.Domain.Entities;

namespace OptiFlow.Infrastructure.Persistence;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class CmsConnectionConfiguration : IEntityTypeConfiguration<CmsConnection>
{
    public void Configure(EntityTypeBuilder<CmsConnection> builder)
    {
        builder.ToTable("cms_connections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CredentialRef).HasColumnName("credential_ref").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Provider });
    }
}

internal sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.ToTable("content_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ConnectionId).HasColumnName("connection_id").IsRequired();
        builder.Property(x => x.ExternalContentKey).HasColumnName("external_content_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceLocale).HasColumnName("source_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.LatestVersionId).HasColumnName("latest_version_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ConnectionId, x.ExternalContentKey }).IsUnique();
        builder.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.ContentItemId);
    }
}

internal sealed class ContentVersionConfiguration(bool isNpgsql) : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("content_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContentItemId).HasColumnName("content_item_id").IsRequired();
        builder.Property(x => x.ExternalVersionKey).HasColumnName("external_version_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RawPayload)
            .HasColumnName("raw_payload")
            .HasColumnType(isNpgsql ? "jsonb" : "TEXT")
            .IsRequired();
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at").IsRequired();

        builder.HasIndex(x => new { x.ContentItemId, x.ExternalVersionKey, x.Locale }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ContentItemId });
        builder.HasMany(x => x.Blocks).WithOne().HasForeignKey(b => b.ContentVersionId);
    }
}

internal sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("content_blocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContentVersionId).HasColumnName("content_version_id").IsRequired();
        builder.Property(x => x.BlockKey).HasColumnName("block_key").HasMaxLength(300).IsRequired();
        builder.Property(x => x.FieldPath).HasColumnName("field_path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.BlockType).HasColumnName("block_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceText).HasColumnName("source_text").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Ordinal).HasColumnName("ordinal").IsRequired();

        builder.HasIndex(x => new { x.ContentVersionId, x.BlockKey }).IsUnique();
        builder.HasIndex(x => new { x.ContentVersionId, x.ContentHash });
        builder.HasIndex(x => new { x.TenantId, x.ContentVersionId });
    }
}

internal sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("translations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContentItemId).HasColumnName("content_item_id").IsRequired();
        builder.Property(x => x.BlockKey).HasColumnName("block_key").HasMaxLength(300).IsRequired();
        builder.Property(x => x.TargetLocale).HasColumnName("target_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceHash).HasColumnName("source_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TranslatedText).HasColumnName("translated_text").IsRequired();
        builder.Property(x => x.Provenance).HasColumnName("provenance").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ContentItemId, x.BlockKey, x.TargetLocale }).IsUnique();
    }
}

internal sealed class LocalizationJobConfiguration : IEntityTypeConfiguration<LocalizationJob>
{
    public void Configure(EntityTypeBuilder<LocalizationJob> builder)
    {
        builder.ToTable("localization_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContentItemId).HasColumnName("content_item_id").IsRequired();
        builder.Property(x => x.SourceVersionId).HasColumnName("source_version_id").IsRequired();
        builder.Property(x => x.PreviousSourceVersionId).HasColumnName("previous_source_version_id");
        builder.Property(x => x.TargetLocale).HasColumnName("target_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.MaxAttempts).HasColumnName("max_attempts").IsRequired();
        builder.Property(x => x.AvailableAt).HasColumnName("available_at").IsRequired();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(200);
        builder.Property(x => x.LockedAt).HasColumnName("locked_at");
        builder.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.AvailableAt });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasMany(x => x.Blocks).WithOne().HasForeignKey(b => b.JobId);
    }
}

internal sealed class LocalizationJobBlockConfiguration(bool isNpgsql) : IEntityTypeConfiguration<LocalizationJobBlock>
{
    public void Configure(EntityTypeBuilder<LocalizationJobBlock> builder)
    {
        builder.ToTable("localization_job_blocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(x => x.BlockKey).HasColumnName("block_key").HasMaxLength(300).IsRequired();
        builder.Property(x => x.FieldPath).HasColumnName("field_path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.BlockType).HasColumnName("block_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceText).HasColumnName("source_text").IsRequired();
        builder.Property(x => x.SourceHash).HasColumnName("source_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ResolutionAction).HasColumnName("resolution_action").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.PreviousTranslatedText).HasColumnName("previous_translated_text");
        builder.Property(x => x.TranslatedText).HasColumnName("translated_text");
        builder.Property(x => x.TranslationMemoryId).HasColumnName("translation_memory_id");
        builder.Property(x => x.HasHumanEditConflict).HasColumnName("has_human_edit_conflict").IsRequired();
        builder.Property(x => x.ValidationResult)
            .HasColumnName("validation_result")
            .HasColumnType(isNpgsql ? "jsonb" : "TEXT");

        builder.HasIndex(x => new { x.JobId, x.BlockKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.JobId });
    }
}

internal sealed class TranslationMemoryEntryConfiguration : IEntityTypeConfiguration<TranslationMemoryEntry>
{
    public void Configure(EntityTypeBuilder<TranslationMemoryEntry> builder)
    {
        builder.ToTable("translation_memory_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SourceLocale).HasColumnName("source_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.TargetLocale).HasColumnName("target_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceHash).HasColumnName("source_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TranslatedText).HasColumnName("translated_text").IsRequired();
        builder.Property(x => x.Provenance).HasColumnName("provenance").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ApprovalStatus).HasColumnName("approval_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.GlossaryVersion).HasColumnName("glossary_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(150).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.ApprovedBy).HasColumnName("approved_by").HasMaxLength(200);

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SourceLocale,
            x.TargetLocale,
            x.SourceHash,
            x.GlossaryVersion,
            x.ContentType
        }).IsUnique();
    }
}

internal sealed class GlossaryTermConfiguration : IEntityTypeConfiguration<GlossaryTerm>
{
    public void Configure(EntityTypeBuilder<GlossaryTerm> builder)
    {
        builder.ToTable("glossary_terms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SourceLocale).HasColumnName("source_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.TargetLocale).HasColumnName("target_locale").HasMaxLength(20).IsRequired();
        builder.Property(x => x.GlossaryVersion).HasColumnName("glossary_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceTerm).HasColumnName("source_term").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PreferredTranslation).HasColumnName("preferred_translation").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ForbiddenTranslation).HasColumnName("forbidden_translation").HasMaxLength(200);

        builder.HasIndex(x => new { x.TenantId, x.SourceLocale, x.TargetLocale, x.GlossaryVersion });
    }
}

internal sealed class ReviewTaskConfiguration : IEntityTypeConfiguration<ReviewTask>
{
    public void Configure(EntityTypeBuilder<ReviewTask> builder)
    {
        builder.ToTable("review_tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(x => x.ReviewerId).HasColumnName("reviewer_id").HasMaxLength(200);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(30);
        builder.Property(x => x.Comments).HasColumnName("comments").HasMaxLength(2000);
        builder.Property(x => x.ExpectedCmsVersion).HasColumnName("expected_cms_version").HasMaxLength(500);
        builder.Property(x => x.CmsDraftId).HasColumnName("cms_draft_id").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");

        builder.HasIndex(x => x.JobId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

internal sealed class AiInvocationConfiguration : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        builder.ToTable("ai_invocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(x => x.JobBlockId).HasColumnName("job_block_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(x => x.OutputTokens).HasColumnName("output_tokens").IsRequired();
        builder.Property(x => x.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(x => x.EstimatedCost).HasColumnName("estimated_cost").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.JobId });
    }
}

internal sealed class AuditEventConfiguration(bool isNpgsql) : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType(isNpgsql ? "jsonb" : "TEXT")
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

internal sealed class WebhookInboxConfiguration : IEntityTypeConfiguration<WebhookInbox>
{
    public void Configure(EntityTypeBuilder<WebhookInbox> builder)
    {
        builder.ToTable("webhook_inbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(300).IsRequired();
        builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(x => new { x.TenantId, x.Provider, x.ExternalEventId }).IsUnique();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.PublishedAt).HasColumnName("published_at");

        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}
