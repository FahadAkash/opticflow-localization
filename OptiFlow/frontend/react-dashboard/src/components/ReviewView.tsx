import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError, optiFlowApi } from '../api/client';
import type { ParsedValidationBreakdown, ReviewBlockOverride } from '../api/types';

function parseValidation(rawJson: string | null): ParsedValidationBreakdown | null {
  if (!rawJson) return null;
  try {
    return JSON.parse(rawJson) as ParsedValidationBreakdown;
  } catch {
    return null;
  }
}

export function ReviewView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const queryClient = useQueryClient();
  const [selectedReviewId, setSelectedReviewId] = useState<string>('');
  const [comments, setComments] = useState('');
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [staleConflictBanner, setStaleConflictBanner] = useState<string | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);

  const reviewsQuery = useQuery({
    queryKey: ['reviews'],
    queryFn: optiFlowApi.listPendingReviews,
    enabled: isAuthenticated,
  });

  const activeReview =
    reviewsQuery.data?.find((r) => r.reviewId === selectedReviewId) ?? reviewsQuery.data?.[0] ?? null;

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
      setSuccessBanner(`CMS Draft created (${decision.cmsDraftId ?? 'draft'}) — never auto-published.`);
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

  return (
    <div>
      {staleConflictBanner && <div className="alert alert-danger">{staleConflictBanner}</div>}
      {successBanner && <div className="alert alert-success">{successBanner}</div>}

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Pending Human Reviews</h3>
          <select
            value={activeReview?.reviewId ?? ''}
            onChange={(e) => {
              setSelectedReviewId(e.target.value);
              setOverrides({});
              setStaleConflictBanner(null);
            }}
            disabled={!reviewsQuery.data?.length}
          >
            {reviewsQuery.data?.map((r) => (
              <option key={r.reviewId} value={r.reviewId}>
                Job {r.jobId.slice(0, 8)} ({r.targetLocale}) — {r.jobStatus}
              </option>
            ))}
          </select>
        </div>

        {!activeReview ? (
          <p>No pending review tasks.</p>
        ) : (
          <>
            <p className="mono">
              Expected CMS ETag/Version: {activeReview.expectedCmsVersion ?? 'N/A'} | Job Status: {activeReview.jobStatus}
            </p>

            {blocksQuery.data?.map((block) => {
              const validation = parseValidation(block.validationResult);
              const overrideValue = overrides[block.blockKey] ?? '';
              return (
                <div key={block.id} className="card" style={{ background: '#fcfcfd' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
                    <span className="mono">
                      <strong>{block.blockKey}</strong> ({block.fieldPath})
                    </span>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <span className="badge badge-blue">{block.actionType}</span>
                      <span className="badge badge-slate">{block.resolutionAction}</span>
                      {block.hasHumanEditConflict && (
                        <span className="badge badge-red">HUMAN EDIT CONFLICT</span>
                      )}
                    </div>
                  </div>

                  {/* Plain-text XSS-safe side-by-side panes (never dangerouslySetInnerHTML) */}
                  <div className="diff-grid">
                    <div>
                      <div className="kpi-label">Master Source</div>
                      <div className="diff-pane">{block.sourceText}</div>
                    </div>
                    <div>
                      <div className="kpi-label">Previous Translation</div>
                      <div className="diff-pane">{block.previousTranslatedText ?? '—'}</div>
                    </div>
                    <div>
                      <div className="kpi-label">Proposed Translation</div>
                      <div className="diff-pane">{block.translatedText ?? '—'}</div>
                    </div>
                  </div>

                  {validation && (
                    <div style={{ marginTop: 10, display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                      {validation.checks.map((chk) => (
                        <span
                          key={chk.checkName}
                          className={chk.passed ? 'badge badge-green' : 'badge badge-red'}
                          title={chk.detail ?? undefined}
                        >
                          {chk.checkName}: {chk.passed ? 'PASS' : 'FAIL'}
                        </span>
                      ))}
                    </div>
                  )}

                  <div style={{ marginTop: 10 }}>
                    <input
                      style={{ width: '100%' }}
                      value={overrideValue}
                      onChange={(e) =>
                        setOverrides((prev) => ({ ...prev, [block.blockKey]: e.target.value }))
                      }
                      placeholder="Optional reviewer edit / conflict resolution text (promotes to HUMAN_APPROVED TM)"
                    />
                  </div>
                </div>
              );
            })}

            <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginTop: 16, flexWrap: 'wrap' }}>
              <input
                style={{ flex: 1, minWidth: 240 }}
                value={comments}
                onChange={(e) => setComments(e.target.value)}
                placeholder="Reviewer comments"
              />
              <button
                type="button"
                className="btn btn-primary"
                disabled={approveMutation.isPending || rejectMutation.isPending}
                onClick={() => approveMutation.mutate()}
              >
                {approveMutation.isPending ? 'Creating CMS Draft...' : 'Approve & Create CMS Draft'}
              </button>
              <button
                type="button"
                className="btn btn-danger"
                disabled={approveMutation.isPending || rejectMutation.isPending}
                onClick={() => rejectMutation.mutate()}
              >
                Reject
              </button>
            </div>

            {approveMutation.isError && !(approveMutation.error instanceof ApiRequestError && approveMutation.error.code === 'STALE_CONFLICT') && (
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
