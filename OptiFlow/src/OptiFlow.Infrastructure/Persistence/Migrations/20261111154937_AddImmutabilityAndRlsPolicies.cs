using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutabilityAndRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_version_update()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'content_versions rows are immutable — insert a new row instead';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_content_versions_no_update
                    BEFORE UPDATE ON content_versions
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_version_update();
                """);

            string[] tenantTables =
            [
                "cms_connections",
                "content_items",
                "content_versions",
                "content_blocks",
                "translations",
                "localization_jobs",
                "localization_job_blocks",
                "translation_memory_entries",
                "glossary_terms",
                "review_tasks",
                "ai_invocations",
                "audit_events",
                "webhook_inbox",
                "outbox_messages"
            ];

            foreach (string table in tenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation_{table} ON {table}
                        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            string[] tenantTables =
            [
                "cms_connections",
                "content_items",
                "content_versions",
                "content_blocks",
                "translations",
                "localization_jobs",
                "localization_job_blocks",
                "translation_memory_entries",
                "glossary_terms",
                "review_tasks",
                "ai_invocations",
                "audit_events",
                "webhook_inbox",
                "outbox_messages"
            ];

            foreach (string table in tenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation_{table} ON {table};
                    ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;
                    """);
            }

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_content_versions_no_update ON content_versions;
                DROP FUNCTION IF EXISTS prevent_version_update();
                """);
        }
    }
}
