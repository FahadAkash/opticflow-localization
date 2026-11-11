using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_invocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_block_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_invocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cms_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    credential_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_content_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    source_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latest_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "glossary_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    glossary_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    preferred_translation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    forbidden_translation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_glossary_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "localization_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_source_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localization_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "review_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expected_cms_version = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cms_draft_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translation_memory_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    translated_text = table.Column<string>(type: "text", nullable: false),
                    provenance = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approval_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    glossary_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_memory_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    target_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    translated_text = table.Column<string>(type: "text", nullable: false),
                    provenance = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_inbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    processing_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_inbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_version_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_content_versions_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "localization_job_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    field_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    block_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_text = table.Column<string>(type: "text", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resolution_action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    previous_translated_text = table.Column<string>(type: "text", nullable: true),
                    translated_text = table.Column<string>(type: "text", nullable: true),
                    translation_memory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    has_human_edit_conflict = table.Column<bool>(type: "boolean", nullable: false),
                    validation_result = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localization_job_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_localization_job_blocks_localization_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "localization_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    field_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    block_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_text = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_content_blocks_content_versions_content_version_id",
                        column: x => x.content_version_id,
                        principalTable: "content_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_invocations_tenant_id_job_id",
                table: "ai_invocations",
                columns: new[] { "tenant_id", "job_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_tenant_id_created_at",
                table: "audit_events",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cms_connections_tenant_id_provider",
                table: "cms_connections",
                columns: new[] { "tenant_id", "provider" });

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_content_version_id_block_key",
                table: "content_blocks",
                columns: new[] { "content_version_id", "block_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_content_version_id_content_hash",
                table: "content_blocks",
                columns: new[] { "content_version_id", "content_hash" });

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_tenant_id_content_version_id",
                table: "content_blocks",
                columns: new[] { "tenant_id", "content_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_content_items_tenant_id_connection_id_external_content_key",
                table: "content_items",
                columns: new[] { "tenant_id", "connection_id", "external_content_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_versions_content_item_id_external_version_key_locale",
                table: "content_versions",
                columns: new[] { "content_item_id", "external_version_key", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_versions_tenant_id_content_item_id",
                table: "content_versions",
                columns: new[] { "tenant_id", "content_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_glossary_terms_tenant_id_source_locale_target_locale_glossa~",
                table: "glossary_terms",
                columns: new[] { "tenant_id", "source_locale", "target_locale", "glossary_version" });

            migrationBuilder.CreateIndex(
                name: "IX_localization_job_blocks_job_id_block_key",
                table: "localization_job_blocks",
                columns: new[] { "job_id", "block_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_localization_job_blocks_tenant_id_job_id",
                table: "localization_job_blocks",
                columns: new[] { "tenant_id", "job_id" });

            migrationBuilder.CreateIndex(
                name: "IX_localization_jobs_status_available_at",
                table: "localization_jobs",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_localization_jobs_tenant_id_idempotency_key",
                table: "localization_jobs",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_localization_jobs_tenant_id_status",
                table: "localization_jobs",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id_status_created_at",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_review_tasks_job_id",
                table: "review_tasks",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_tasks_tenant_id_status",
                table: "review_tasks",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_memory_entries_tenant_id_source_locale_target_l~",
                table: "translation_memory_entries",
                columns: new[] { "tenant_id", "source_locale", "target_locale", "source_hash", "glossary_version", "content_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translations_tenant_id_content_item_id_block_key_target_loc~",
                table: "translations",
                columns: new[] { "tenant_id", "content_item_id", "block_key", "target_locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_inbox_tenant_id_provider_external_event_id",
                table: "webhook_inbox",
                columns: new[] { "tenant_id", "provider", "external_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_invocations");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "cms_connections");

            migrationBuilder.DropTable(
                name: "content_blocks");

            migrationBuilder.DropTable(
                name: "glossary_terms");

            migrationBuilder.DropTable(
                name: "localization_job_blocks");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "review_tasks");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "translation_memory_entries");

            migrationBuilder.DropTable(
                name: "translations");

            migrationBuilder.DropTable(
                name: "webhook_inbox");

            migrationBuilder.DropTable(
                name: "content_versions");

            migrationBuilder.DropTable(
                name: "localization_jobs");

            migrationBuilder.DropTable(
                name: "content_items");
        }
    }
}
