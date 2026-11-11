import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { optiFlowApi } from '../api/client';
import type { JobStatus } from '../api/types';

const ACTIVE_STATES: ReadonlySet<JobStatus> = new Set([
  'Queued',
  'Snapshotting',
  'Diffing',
  'Translating',
  'Validating',
  'RetryWait',
]);

function jobBadgeClass(status: JobStatus): string {
  switch (status) {
    case 'CmsDraftCreated':
    case 'Approved':
      return 'badge badge-green';
    case 'AwaitingReview':
    case 'Queued':
    case 'Snapshotting':
    case 'Diffing':
    case 'Translating':
    case 'Validating':
      return 'badge badge-blue';
    case 'NeedsManualReview':
    case 'RetryWait':
    case 'StaleConflict':
      return 'badge badge-amber';
    case 'Rejected':
    case 'Failed':
    case 'DeadLetter':
      return 'badge badge-red';
  }
}

export function OperationsView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const queryClient = useQueryClient();
  const [targetLocales, setTargetLocales] = useState('es-ES, fr-FR');
  const [selectedContentId, setSelectedContentId] = useState('');

  const itemsQuery = useQuery({
    queryKey: ['content-items'],
    queryFn: optiFlowApi.listContentItems,
    enabled: isAuthenticated,
  });

  const jobsQuery = useQuery({
    queryKey: ['jobs'],
    queryFn: optiFlowApi.listJobs,
    enabled: isAuthenticated,
    refetchInterval: (query) => {
      const hasActive = query.state.data?.some((j) => ACTIVE_STATES.has(j.status)) ?? false;
      return hasActive ? 3000 : false;
    },
  });

  const effectiveContentId = selectedContentId || itemsQuery.data?.[0]?.id || '';

  const createJobMutation = useMutation({
    mutationFn: () => {
      const item = itemsQuery.data?.find((i) => i.id === effectiveContentId);
      if (!item || !item.latestVersionId) {
        throw new Error('Select a synced content item with a captured version.');
      }
      const locales = targetLocales
        .split(',')
        .map((l) => l.trim())
        .filter((l) => l.length > 0);
      return optiFlowApi.createJob(item.id, item.latestVersionId, locales);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['jobs'] });
    },
  });

  return (
    <div>
      <div className="card">
        <h3>Queue New Localization Job (Async 202 Accepted)</h3>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
          <select
            value={effectiveContentId}
            onChange={(e) => setSelectedContentId(e.target.value)}
            disabled={!itemsQuery.data?.length}
          >
            {itemsQuery.data?.map((item) => (
              <option key={item.id} value={item.id}>
                {item.externalContentKey}
              </option>
            ))}
          </select>
          <input
            value={targetLocales}
            onChange={(e) => setTargetLocales(e.target.value)}
            placeholder="Target locales (comma-separated)"
          />
          <button
            type="button"
            className="btn btn-primary"
            disabled={!isAuthenticated || !effectiveContentId || createJobMutation.isPending}
            onClick={() => createJobMutation.mutate()}
          >
            {createJobMutation.isPending ? 'Queuing...' : 'Queue Localization'}
          </button>
        </div>
        {createJobMutation.isError && (
          <div className="alert alert-danger" style={{ marginTop: 12 }}>
            {(createJobMutation.error as Error).message}
          </div>
        )}
      </div>

      <div className="card">
        <h3>Localization Job Queue</h3>
        <table className="table">
          <thead>
            <tr>
              <th>Job ID</th>
              <th>Target Locale</th>
              <th>Status</th>
              <th>Attempts</th>
              <th>Last Error</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {jobsQuery.data?.map((job) => (
              <tr key={job.jobId}>
                <td className="mono">{job.jobId.slice(0, 8)}...</td>
                <td>{job.targetLocale}</td>
                <td>
                  <span className={jobBadgeClass(job.status)}>{job.status}</span>
                </td>
                <td>
                  {job.attemptCount} / {job.maxAttempts}
                </td>
                <td className="mono">{job.lastErrorCode ?? '—'}</td>
                <td>{new Date(job.createdAt).toLocaleString()}</td>
              </tr>
            ))}
            {(!jobsQuery.data || jobsQuery.data.length === 0) && (
              <tr>
                <td colSpan={6}>No localization jobs queued yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
