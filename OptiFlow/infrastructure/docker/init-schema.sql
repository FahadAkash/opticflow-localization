CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE ai_invocations (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        job_id uuid NOT NULL,
        job_block_id uuid NOT NULL,
        provider character varying(100) NOT NULL,
        model_name character varying(200) NOT NULL,
        prompt_version character varying(100) NOT NULL,
        input_tokens integer NOT NULL,
        output_tokens integer NOT NULL,
        latency_ms integer NOT NULL,
        estimated_cost numeric(18,6) NOT NULL,
        status character varying(40) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ai_invocations" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE audit_events (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        actor_id character varying(200) NOT NULL,
        action character varying(100) NOT NULL,
        resource_type character varying(100) NOT NULL,
        resource_id character varying(200) NOT NULL,
        metadata jsonb NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_events" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE cms_connections (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        provider character varying(50) NOT NULL,
        base_url character varying(500) NOT NULL,
        credential_ref character varying(2000) NOT NULL,
        status character varying(30) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_cms_connections" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE content_items (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        connection_id uuid NOT NULL,
        external_content_key character varying(500) NOT NULL,
        content_type character varying(150) NOT NULL,
        source_locale character varying(20) NOT NULL,
        latest_version_id uuid,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_content_items" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE glossary_terms (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        source_locale character varying(20) NOT NULL,
        target_locale character varying(20) NOT NULL,
        glossary_version character varying(50) NOT NULL,
        source_term character varying(200) NOT NULL,
        preferred_translation character varying(200) NOT NULL,
        forbidden_translation character varying(200),
        CONSTRAINT "PK_glossary_terms" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE localization_jobs (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        content_item_id uuid NOT NULL,
        source_version_id uuid NOT NULL,
        previous_source_version_id uuid,
        target_locale character varying(20) NOT NULL,
        status character varying(40) NOT NULL,
        idempotency_key character varying(300) NOT NULL,
        priority integer NOT NULL,
        attempt_count integer NOT NULL,
        max_attempts integer NOT NULL,
        available_at timestamp with time zone NOT NULL,
        locked_by character varying(200),
        locked_at timestamp with time zone,
        lease_expires_at timestamp with time zone,
        last_error_code character varying(100),
        trace_id character varying(64),
        created_at timestamp with time zone NOT NULL,
        completed_at timestamp with time zone,
        CONSTRAINT "PK_localization_jobs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE outbox_messages (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        aggregate_id uuid NOT NULL,
        event_type character varying(150) NOT NULL,
        payload text NOT NULL,
        status character varying(30) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        published_at timestamp with time zone,
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE review_tasks (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        job_id uuid NOT NULL,
        reviewer_id character varying(200),
        status character varying(30) NOT NULL,
        decision character varying(30),
        comments character varying(2000),
        expected_cms_version character varying(500),
        cms_draft_id character varying(500),
        created_at timestamp with time zone NOT NULL,
        reviewed_at timestamp with time zone,
        CONSTRAINT "PK_review_tasks" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE tenants (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        slug character varying(100) NOT NULL,
        status character varying(30) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE translation_memory_entries (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        source_locale character varying(20) NOT NULL,
        target_locale character varying(20) NOT NULL,
        source_hash character varying(64) NOT NULL,
        translated_text text NOT NULL,
        provenance character varying(30) NOT NULL,
        approval_status character varying(30) NOT NULL,
        glossary_version character varying(50) NOT NULL,
        content_type character varying(150) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        approved_at timestamp with time zone,
        approved_by character varying(200),
        CONSTRAINT "PK_translation_memory_entries" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE translations (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        content_item_id uuid NOT NULL,
        block_key character varying(300) NOT NULL,
        target_locale character varying(20) NOT NULL,
        source_hash character varying(64) NOT NULL,
        translated_text text NOT NULL,
        provenance character varying(30) NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_translations" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE webhook_inbox (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        provider character varying(100) NOT NULL,
        external_event_id character varying(300) NOT NULL,
        payload_hash character varying(64) NOT NULL,
        processing_status character varying(30) NOT NULL,
        received_at timestamp with time zone NOT NULL,
        processed_at timestamp with time zone,
        CONSTRAINT "PK_webhook_inbox" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE content_versions (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        content_item_id uuid NOT NULL,
        external_version_key character varying(500) NOT NULL,
        locale character varying(20) NOT NULL,
        version_number integer NOT NULL,
        content_hash character varying(64) NOT NULL,
        raw_payload jsonb NOT NULL,
        captured_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_content_versions" PRIMARY KEY (id),
        CONSTRAINT "FK_content_versions_content_items_content_item_id" FOREIGN KEY (content_item_id) REFERENCES content_items (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE localization_job_blocks (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        job_id uuid NOT NULL,
        block_key character varying(300) NOT NULL,
        field_path character varying(500) NOT NULL,
        block_type character varying(100) NOT NULL,
        source_text text NOT NULL,
        source_hash character varying(64) NOT NULL,
        action_type character varying(30) NOT NULL,
        resolution_action character varying(40) NOT NULL,
        status character varying(40) NOT NULL,
        previous_translated_text text,
        translated_text text,
        translation_memory_id uuid,
        has_human_edit_conflict boolean NOT NULL,
        validation_result jsonb,
        CONSTRAINT "PK_localization_job_blocks" PRIMARY KEY (id),
        CONSTRAINT "FK_localization_job_blocks_localization_jobs_job_id" FOREIGN KEY (job_id) REFERENCES localization_jobs (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE TABLE content_blocks (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        content_version_id uuid NOT NULL,
        block_key character varying(300) NOT NULL,
        field_path character varying(500) NOT NULL,
        block_type character varying(100) NOT NULL,
        source_text text NOT NULL,
        content_hash character varying(64) NOT NULL,
        ordinal integer NOT NULL,
        CONSTRAINT "PK_content_blocks" PRIMARY KEY (id),
        CONSTRAINT "FK_content_blocks_content_versions_content_version_id" FOREIGN KEY (content_version_id) REFERENCES content_versions (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_ai_invocations_tenant_id_job_id" ON ai_invocations (tenant_id, job_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_audit_events_tenant_id_created_at" ON audit_events (tenant_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_cms_connections_tenant_id_provider" ON cms_connections (tenant_id, provider);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_content_blocks_content_version_id_block_key" ON content_blocks (content_version_id, block_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_content_blocks_content_version_id_content_hash" ON content_blocks (content_version_id, content_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_content_blocks_tenant_id_content_version_id" ON content_blocks (tenant_id, content_version_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_content_items_tenant_id_connection_id_external_content_key" ON content_items (tenant_id, connection_id, external_content_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_content_versions_content_item_id_external_version_key_locale" ON content_versions (content_item_id, external_version_key, locale);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_content_versions_tenant_id_content_item_id" ON content_versions (tenant_id, content_item_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_glossary_terms_tenant_id_source_locale_target_locale_glossa~" ON glossary_terms (tenant_id, source_locale, target_locale, glossary_version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_localization_job_blocks_job_id_block_key" ON localization_job_blocks (job_id, block_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_localization_job_blocks_tenant_id_job_id" ON localization_job_blocks (tenant_id, job_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_localization_jobs_status_available_at" ON localization_jobs (status, available_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_localization_jobs_tenant_id_idempotency_key" ON localization_jobs (tenant_id, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_localization_jobs_tenant_id_status" ON localization_jobs (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_outbox_messages_tenant_id_status_created_at" ON outbox_messages (tenant_id, status, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_review_tasks_job_id" ON review_tasks (job_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE INDEX "IX_review_tasks_tenant_id_status" ON review_tasks (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_tenants_slug" ON tenants (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_translation_memory_entries_tenant_id_source_locale_target_l~" ON translation_memory_entries (tenant_id, source_locale, target_locale, source_hash, glossary_version, content_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_translations_tenant_id_content_item_id_block_key_target_loc~" ON translations (tenant_id, content_item_id, block_key, target_locale);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_webhook_inbox_tenant_id_provider_external_event_id" ON webhook_inbox (tenant_id, provider, external_event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154831_InitialSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111154831_InitialSchema', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE cms_connections ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_cms_connections ON cms_connections
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE content_items ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_content_items ON content_items
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE content_versions ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_content_versions ON content_versions
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE content_blocks ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_content_blocks ON content_blocks
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE translations ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_translations ON translations
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE localization_jobs ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_localization_jobs ON localization_jobs
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE localization_job_blocks ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_localization_job_blocks ON localization_job_blocks
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE translation_memory_entries ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_translation_memory_entries ON translation_memory_entries
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE glossary_terms ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_glossary_terms ON glossary_terms
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE review_tasks ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_review_tasks ON review_tasks
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE ai_invocations ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_ai_invocations ON ai_invocations
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_audit_events ON audit_events
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE webhook_inbox ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_webhook_inbox ON webhook_inbox
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    ALTER TABLE outbox_messages ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation_outbox_messages ON outbox_messages
        USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111154937_AddImmutabilityAndRlsPolicies') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111154937_AddImmutabilityAndRlsPolicies', '8.0.11');
    END IF;
END $EF$;
COMMIT;

