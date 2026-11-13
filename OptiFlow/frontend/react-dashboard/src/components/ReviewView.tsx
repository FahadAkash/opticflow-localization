import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Bot,
  ChevronRight,
  Filter,
  Send,
  ShieldCheck,
  XCircle,
  Zap,
} from 'lucide-react';
import { ApiRequestError, optiFlowApi } from '../api/client';
import type { DeterministicCheckResult, ParsedValidationBreakdown, ReviewBlockOverride } from '../api/types';
import { formatLocaleLabel } from '../utils/diff';
import { BlockDiffCard } from './BlockDiffCard';

type FilterTab = 'all' | 'needs-attention' | 'ai' | 'tm' | 'unchanged';

function parseValidation(rawJson: string | null): ParsedValidationBreakdown | null {
  if (!rawJson) return null;
  try {
    const raw = JSON.parse(rawJson);
    if (!raw || typeof raw !== 'object') return null;

    // Handle both camelCase and PascalCase from .NET serialization
    const rawChecks = Array.isArray(raw.checks)
      ? raw.checks
      : Array.isArray(raw.Checks)
        ? raw.Checks
        : [];

    const checks: DeterministicCheckResult[] = rawChecks.map((chk: Record<string, unknown>) => ({
      checkName: String(chk.checkName ?? chk.CheckName ?? 'Check'),
      passed: Boolean(chk.passed ?? chk.Passed),
      detail: (chk.detail ?? chk.Detail ?? null) as string | null,
    }));

    return {
      isValid: Boolean(raw.isValid ?? raw.IsValid),
      checks,
      failureSummary: (raw.failureSummary ?? raw.FailureSummary ?? null) as string | null,
    };
  } catch {
    return null;
  }
}

export function ReviewView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const queryClient = useQueryClient();
  const [selectedReviewId, setSelectedReviewId] = useState<string>('');
  const [reviewStatusFilter, setReviewStatusFilter] = useState<'Pending' | 'Approved'>('Pending');
  const [comments, setComments] = useState('');
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [activeFilter, setActiveFilter] = useState<FilterTab>('all');
  const [staleConflictBanner, setStaleConflictBanner] = useState<string | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);

  const itemsQuery = useQuery({
    queryKey: ['content-items'],
    queryFn: optiFlowApi.listContentItems,
    enabled: isAuthenticated,
  });

  const reviewsQuery = useQuery({
    queryKey: ['reviews', reviewStatusFilter],
    queryFn: () => optiFlowApi.listReviews(reviewStatusFilter),
    enabled: isAuthenticated,
    refetchInterval: (query) => {
      const hasActive = query.state.data?.some(
        (r) =>
          r.jobStatus === 'Queued' ||
          r.jobStatus === 'Snapshotting' ||
          r.jobStatus === 'Diffing' ||
          r.jobStatus === 'Translating' ||
          r.jobStatus === 'Validating' ||
          r.jobStatus === 'StaleConflict'
      );
      return hasActive ? 2500 : false;
    },
  });

  const activeReview =
    reviewsQuery.data?.find((r) => r.reviewId === selectedReviewId) ?? reviewsQuery.data?.[0] ?? null;

  const isJobActionable =
    activeReview?.jobStatus === 'AwaitingReview' || activeReview?.jobStatus === 'NeedsManualReview';

  const activeContentItem = itemsQuery.data?.find((i) => i.id === activeReview?.contentItemId) ?? null;

  const blocksQuery = useQuery({
    queryKey: ['job-blocks', activeReview?.jobId ?? ''],
    queryFn: () => optiFlowApi.getJobBlocks(activeReview!.jobId),
    enabled: isAuthenticated && activeReview !== null,
  });

  const approveMutation = useMutation({
    mutationFn: () => {
      if (!activeReview) throw new Error('No review selected.');
      const blockOverrides: ReviewBlockOverride[] = Object.entries(overrides)
        .filter(([, text]) => text.trim().length > 0)
        .map(([blockKey, approvedText]) => ({ blockKey, approvedText }));
      return optiFlowApi.approveReview(activeReview.reviewId, comments, blockOverrides);
    },
    onSuccess: (decision) => {
      setStaleConflictBanner(null);
      setSuccessBanner(
        `Draft created in Optimizely CMS (Draft ID: ${decision.cmsDraftId ?? 'cms-draft-v1'}). Approved entries saved to Translation Memory.`
      );
      setOverrides({});
      setComments('');
      void queryClient.invalidateQueries({ queryKey: ['reviews'] });
      void queryClient.invalidateQueries({ queryKey: ['jobs'] });
      void queryClient.invalidateQueries({ queryKey: ['analytics'] });
    },
    onError: (err) => {
      setSuccessBanner(null);
      if (err instanceof ApiRequestError && err.status === 409 && err.code === 'STALE_CONFLICT') {
        setStaleConflictBanner(
          '409 STALE_CONFLICT: CMS master content changed during review. OptiFlow has re-synced the latest CMS version and restarted diffing before any draft can be written.'
        );
        void queryClient.invalidateQueries({ queryKey: ['reviews'] });
        void queryClient.invalidateQueries({ queryKey: ['jobs'] });
      }
    },
  });

  const rejectMutation = useMutation({
    mutationFn: () => {
      if (!activeReview) throw new Error('No review selected.');
      return optiFlowApi.rejectReview(activeReview.reviewId, comments);
    },
    onSuccess: () => {
      setStaleConflictBanner(null);
      setSuccessBanner('Review rejected. Job marked terminal REJECTED and will never be published.');
      void queryClient.invalidateQueries({ queryKey: ['reviews'] });
      void queryClient.invalidateQueries({ queryKey: ['jobs'] });
    },
  });

  const blocks = blocksQuery.data ?? [];

  // Metrics computation for the executive bar
  const totalCount = blocks.length;
  const aiCount = blocks.filter((b) => b.resolutionAction === 'AiTranslated').length;
  const tmCount = blocks.filter((b) => b.resolutionAction === 'ReusedTranslationMemory').length;
  const unchangedCount = blocks.filter(
    (b) => b.resolutionAction === 'ReusedUnchanged' || b.resolutionAction === 'PreservedHumanEdit'
  ).length;
  const conflictCount = blocks.filter(
    (b) => b.hasHumanEditConflict || b.resolutionAction === 'ConflictRequiresReview'
  ).length;

  // Filter blocks
  const filteredBlocks = blocks.filter((b) => {
    switch (activeFilter) {
      case 'needs-attention':
        return b.hasHumanEditConflict || b.resolutionAction === 'ConflictRequiresReview';
      case 'ai':
        return b.resolutionAction === 'AiTranslated';
      case 'tm':
        return b.resolutionAction === 'ReusedTranslationMemory';
      case 'unchanged':
        return b.resolutionAction === 'ReusedUnchanged' || b.resolutionAction === 'PreservedHumanEdit';
      case 'all':
      default:
        return true;
    }
  });

  const targetLocaleInfo = activeReview ? formatLocaleLabel(activeReview.targetLocale) : null;
  const overrideCount = Object.values(overrides).filter((t) => t.trim().length > 0).length;

  const conflictOrFailedBlocks = blocks.filter(
    (b) => b.hasHumanEditConflict || b.status === 'VALIDATION_FAILED' || b.status === 'CONFLICT'
  );
  const unresolvedBlocks = conflictOrFailedBlocks.filter(
    (b) => !overrides[b.blockKey] || (overrides[b.blockKey]?.trim().length ?? 0) === 0
  );

  function handleOverrideChange(blockKey: string, value: string) {
    setOverrides((prev) => ({ ...prev, [blockKey]: value }));
  }

  return (
    <div>
      {staleConflictBanner && <div className="alert alert-danger">{staleConflictBanner}</div>}
      {successBanner && <div className="alert alert-success">{successBanner}</div>}

      {/* Main Review Card */}
      <div className="card">
        {/* Top Control Bar: Job Selector */}
        <div className="review-top-bar">
          <div>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
              <span>Translation Review & Human Approval</span>
              {activeReview && (
                <span className="badge badge-blue" style={{ fontSize: '0.8rem' }}>
                  {activeReview.jobStatus}
                </span>
              )}
            </h3>
            <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '0.875rem' }}>
              Review proposed AI translations, verify automated quality checks, and approve draft creation.
            </p>
          </div>

          <div style={{ minWidth: 320 }}>
            <div style={{ display: 'flex', gap: 6, marginBottom: 8 }}>
              <button
                type="button"
                className={`btn btn-sm ${reviewStatusFilter === 'Pending' ? 'btn-primary' : 'btn-outline'}`}
                style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                onClick={() => {
                  setReviewStatusFilter('Pending');
                  setSelectedReviewId('');
                }}
              >
                Pending Reviews
              </button>
              <button
                type="button"
                className={`btn btn-sm ${reviewStatusFilter === 'Approved' ? 'btn-primary' : 'btn-outline'}`}
                style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                onClick={() => {
                  setReviewStatusFilter('Approved');
                  setSelectedReviewId('');
                }}
              >
                Approved History
              </button>
            </div>
            <label style={{ fontSize: '0.78rem', color: '#64748b', display: 'block', marginBottom: 4 }}>
              {reviewStatusFilter === 'Pending' ? 'Select Pending Review Task:' : 'Select Approved Review Record:'}
            </label>
            <select
              style={{ width: '100%', fontWeight: 500 }}
              value={activeReview?.reviewId ?? ''}
              onChange={(e) => {
                setSelectedReviewId(e.target.value);
                setOverrides({});
                setStaleConflictBanner(null);
                setSuccessBanner(null);
              }}
              disabled={!reviewsQuery.data?.length}
            >
              {reviewsQuery.data?.map((r) => {
                const item = itemsQuery.data?.find((i) => i.id === r.contentItemId);
                const locale = formatLocaleLabel(r.targetLocale);
                return (
                  <option key={r.reviewId} value={r.reviewId}>
                    {item ? `${item.externalContentKey} (${item.contentType})` : `Item ${r.contentItemId.slice(0, 8)}`}{' '}
                    ➔ {locale.flag} {locale.name} (Job {r.jobId.slice(0, 8)})
                  </option>
                );
              })}
            </select>
          </div>
        </div>

        {!activeReview ? (
          <div style={{ textAlign: 'center', padding: '40px 20px', color: '#64748b' }}>
            <p>No jobs currently awaiting human review.</p>
          </div>
        ) : (
          <>
            {/* Job Metadata Banner */}
            <div className="job-meta-banner">
              <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
                <div>
                  <span className="meta-label">CONTENT ITEM</span>
                  <div className="meta-value">
                    {activeContentItem ? (
                      <>
                        <strong>{activeContentItem.externalContentKey}</strong>{' '}
                        <span style={{ color: '#64748b' }}>({activeContentItem.contentType})</span>
                      </>
                    ) : (
                      <span className="mono">{activeReview.contentItemId}</span>
                    )}
                  </div>
                </div>

                <div className="meta-divider" />

                <div>
                  <span className="meta-label">TRANSLATION ROUTE</span>
                  <div className="meta-value">
                    <span>🇺🇸 English (en)</span>
                    <ChevronRight size={14} style={{ display: 'inline', margin: '0 4px', verticalAlign: 'middle' }} />
                    <strong>
                      {targetLocaleInfo?.flag} {targetLocaleInfo?.name}
                    </strong>
                  </div>
                </div>

                <div className="meta-divider" />

                <div>
                  <span className="meta-label">EXPECTED CMS ETAG</span>
                  <div className="meta-value mono">{activeReview.expectedCmsVersion ?? 'N/A'}</div>
                </div>

                <div className="meta-divider" />

                <div>
                  <span className="meta-label">GUARDRAIL</span>
                  <div className="meta-value" style={{ color: '#166534', fontWeight: 600 }}>
                    🛡️ Draft-Only (Never Auto-Published)
                  </div>
                </div>
              </div>
            </div>

            {/* Incremental Overview Bar (What is changed vs not changed) */}
            <div className="summary-metric-bar">
              <div className="metric-item">
                <span className="metric-number">{totalCount}</span>
                <span className="metric-desc">Total Blocks</span>
              </div>

              <div className="metric-item metric-item-ai">
                <span className="metric-number">
                  <Bot size={15} style={{ marginRight: 4, verticalAlign: 'middle' }} />
                  {aiCount}
                </span>
                <span className="metric-desc">AI Translated (New/Modified)</span>
              </div>

              <div className="metric-item metric-item-tm">
                <span className="metric-number">
                  <Zap size={15} style={{ marginRight: 4, verticalAlign: 'middle' }} />
                  {tmCount}
                </span>
                <span className="metric-desc">100% TM Reuse ($0 AI Cost)</span>
              </div>

              <div className="metric-item metric-item-unchanged">
                <span className="metric-number">
                  <ShieldCheck size={15} style={{ marginRight: 4, verticalAlign: 'middle' }} />
                  {unchangedCount}
                </span>
                <span className="metric-desc">Preserved / Unchanged</span>
              </div>

              {conflictCount > 0 && (
                <div className="metric-item metric-item-conflict">
                  <span className="metric-number">
                    <AlertTriangle size={15} style={{ marginRight: 4, verticalAlign: 'middle' }} />
                    {conflictCount}
                  </span>
                  <span className="metric-desc">Review Required</span>
                </div>
              )}
            </div>

            {/* Filter Tabs */}
            <div className="filter-pill-bar">
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <span style={{ fontSize: '0.8rem', color: '#64748b', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <Filter size={13} /> Filter View:
                </span>
                <button
                  type="button"
                  className={`filter-pill ${activeFilter === 'all' ? 'active' : ''}`}
                  onClick={() => setActiveFilter('all')}
                >
                  All Blocks ({totalCount})
                </button>
                {conflictCount > 0 && (
                  <button
                    type="button"
                    className={`filter-pill filter-pill-conflict ${activeFilter === 'needs-attention' ? 'active' : ''}`}
                    onClick={() => setActiveFilter('needs-attention')}
                  >
                    ⚠️ Needs Attention ({conflictCount})
                  </button>
                )}
                <button
                  type="button"
                  className={`filter-pill ${activeFilter === 'ai' ? 'active' : ''}`}
                  onClick={() => setActiveFilter('ai')}
                >
                  🤖 AI Translated ({aiCount})
                </button>
                <button
                  type="button"
                  className={`filter-pill ${activeFilter === 'tm' ? 'active' : ''}`}
                  onClick={() => setActiveFilter('tm')}
                >
                  ⚡ TM Reused ({tmCount})
                </button>
                <button
                  type="button"
                  className={`filter-pill ${activeFilter === 'unchanged' ? 'active' : ''}`}
                  onClick={() => setActiveFilter('unchanged')}
                >
                  🔒 Unchanged ({unchangedCount})
                </button>
              </div>

              {overrideCount > 0 && (
                <span className="badge badge-purple" style={{ fontSize: '0.78rem' }}>
                  {overrideCount} manual edit{overrideCount > 1 ? 's' : ''} queued
                </span>
              )}
            </div>

            {/* Unresolved Conflicts Warning Banner */}
            {unresolvedBlocks.length > 0 && isJobActionable && (
              <div
                className="alert alert-warning"
                style={{
                  marginTop: 14,
                  marginBottom: 14,
                  padding: '12px 16px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: 12,
                  backgroundColor: '#fffbeb',
                  border: '1px solid #fde68a',
                  borderRadius: 8,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <AlertTriangle size={22} color="#b45309" style={{ flexShrink: 0 }} />
                  <div>
                    <strong style={{ color: '#92400e', fontSize: '0.92rem' }}>
                      {unresolvedBlocks.length} Unresolved Block(s) Require Human Decision Before Approval:
                    </strong>
                    <div style={{ fontSize: '0.8rem', color: '#78350f', marginTop: 2 }}>
                      Blocks requiring action:{' '}
                      {unresolvedBlocks.map((b) => (
                        <code key={b.blockKey} style={{ marginRight: 6, fontWeight: 700 }}>
                          {b.blockKey} ({b.hasHumanEditConflict ? 'Conflict' : 'Validation Failed'})
                        </code>
                      ))}
                    </div>
                  </div>
                </div>

                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  style={{ fontSize: '0.8rem', fontWeight: 600 }}
                  onClick={() => {
                    const nextOverrides = { ...overrides };
                    for (const b of unresolvedBlocks) {
                      if (b.translatedText) {
                        nextOverrides[b.blockKey] = b.translatedText;
                      } else if (b.sourceText) {
                        nextOverrides[b.blockKey] = b.sourceText;
                      }
                    }
                    setOverrides(nextOverrides);
                  }}
                >
                  ⚡ Quick Accept Proposed for All ({unresolvedBlocks.length})
                </button>
              </div>
            )}

            {/* Blocks List */}
            <div style={{ marginTop: 16 }}>
              {blocksQuery.isLoading && <p>Loading content blocks...</p>}

              {filteredBlocks.map((block) => {
                const validation = parseValidation(block.validationResult);
                const overrideValue = overrides[block.blockKey] ?? '';

                return (
                  <BlockDiffCard
                    key={block.id}
                    block={block}
                    targetLocale={activeReview.targetLocale}
                    validation={validation}
                    overrideValue={overrideValue}
                    onOverrideChange={handleOverrideChange}
                  />
                );
              })}

              {filteredBlocks.length === 0 && !blocksQuery.isLoading && (
                <div className="card" style={{ textAlign: 'center', padding: '24px', color: '#64748b' }}>
                  No blocks match the selected filter. Switch to <strong>All Blocks</strong> to inspect all content.
                </div>
              )}
            </div>

            {/* Bottom Actions Bar */}
            {activeReview.status === 'Approved' ? (
              <div className="review-action-dock" style={{ backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: '#166534', flex: 1 }}>
                  <ShieldCheck size={20} />
                  <div>
                    <strong>Review Approved by Human Editor:</strong> Draft created in Optimizely CMS (Draft ID:{' '}
                    <code>{activeReview.cmsDraftId ?? 'cms-draft-v1'}</code>). All approved translations are protected in Translation Memory.
                  </div>
                </div>
              </div>
            ) : (
              <div>
                {!isJobActionable && (
                  <div
                    style={{
                      marginBottom: 12,
                      padding: '10px 14px',
                      borderRadius: 6,
                      background: '#fef3c7',
                      border: '1px solid #fde68a',
                      color: '#92400e',
                      fontSize: '0.875rem',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                    }}
                  >
                    <span>
                      ⏳ <strong>Job is currently {activeReview.jobStatus}:</strong> OptiFlow is actively processing or updating this job. Approval actions will automatically activate once automated validation completes.
                    </span>
                  </div>
                )}

                <div className="review-action-dock">
                  <div style={{ flex: 1, minWidth: 280 }}>
                    <input
                      style={{ width: '100%', padding: '10px 14px' }}
                      value={comments}
                      onChange={(e) => setComments(e.target.value)}
                      placeholder="Optional review audit comments (e.g. 'Verified German grammar and brand tone')..."
                    />
                  </div>

                  <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                    <button
                      type="button"
                      className="btn btn-primary"
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 6,
                        padding: '10px 18px',
                        fontWeight: 600,
                        backgroundColor: unresolvedBlocks.length > 0 ? '#64748b' : undefined,
                        borderColor: unresolvedBlocks.length > 0 ? '#64748b' : undefined,
                      }}
                      disabled={approveMutation.isPending || rejectMutation.isPending || !isJobActionable || unresolvedBlocks.length > 0}
                      onClick={() => approveMutation.mutate()}
                      title={unresolvedBlocks.length > 0 ? `Resolve ${unresolvedBlocks.length} block(s) above before approving` : undefined}
                    >
                      <Send size={15} />
                      {approveMutation.isPending
                        ? 'Writing Draft to CMS...'
                        : !isJobActionable
                          ? `Processing (${activeReview.jobStatus})...`
                          : unresolvedBlocks.length > 0
                            ? `Resolve ${unresolvedBlocks.length} Block(s) to Approve`
                            : 'Approve & Create CMS Draft'}
                    </button>

                    <button
                      type="button"
                      className="btn btn-danger"
                      style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '10px 16px' }}
                      disabled={approveMutation.isPending || rejectMutation.isPending || !isJobActionable}
                      onClick={() => rejectMutation.mutate()}
                    >
                      <XCircle size={15} />
                      Reject
                    </button>
                  </div>
                </div>
              </div>
            )}

            {approveMutation.isError &&
              !(approveMutation.error instanceof ApiRequestError && approveMutation.error.code === 'STALE_CONFLICT') && (
                <div className="alert alert-danger" style={{ marginTop: 12 }}>
                  {(approveMutation.error as Error).message}
                </div>
              )}
          </>
        )}
      </div>
    </div>
  );
}
