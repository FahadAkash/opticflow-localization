import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, AlertTriangle, CheckCircle2, Globe, Sparkles } from 'lucide-react';
import { optiFlowApi } from '../api/client';
import type { JobStatus } from '../api/types';
import { formatLocaleLabel } from '../utils/diff';

const ACTIVE_STATES: ReadonlySet<JobStatus> = new Set([
  'Queued',
  'Snapshotting',
  'Diffing',
  'Translating',
  'Validating',
  'RetryWait',
]);

const LOCALE_REGEX = /^[a-zA-Z]{2,3}(?:-[a-zA-Z]{2,4})?(?:-[a-zA-Z0-9]{2,3})?$/;

const PRESET_LOCALES = [
  { code: 'es-ES', flag: '🇪🇸', label: 'Spanish (es-ES)' },
  { code: 'fr-FR', flag: '🇫🇷', label: 'French (fr-FR)' },
  { code: 'de-DE', flag: '🇩🇪', label: 'German (de-DE)' },
  { code: 'BD', flag: '🇧🇩', label: 'Bengali (BD)' },
  { code: 'ja-JP', flag: '🇯🇵', label: 'Japanese (ja-JP)' },
];

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
  const [successBanner, setSuccessBanner] = useState<string | null>(null);

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
      return hasActive ? 2500 : false;
    },
  });

  const effectiveContentId = selectedContentId || itemsQuery.data?.[0]?.id || '';

  // Strict Client-Side Locale Validation & Injection Protection
  const rawLocaleTokens = targetLocales
    .split(',')
    .map((l) => l.trim())
    .filter((l) => l.length > 0);

  const invalidLocales = rawLocaleTokens.filter(
    (l) => !LOCALE_REGEX.test(l) || l.length > 15
  );

  const hasInvalidLocales = invalidLocales.length > 0;
  const isFormValid = rawLocaleTokens.length > 0 && !hasInvalidLocales;

  const createJobMutation = useMutation({
    mutationFn: () => {
      const item = itemsQuery.data?.find((i) => i.id === effectiveContentId);
      if (!item || !item.latestVersionId) {
        throw new Error('Select a synced content item with a captured version.');
      }
      if (hasInvalidLocales) {
        throw new Error(`Invalid locale(s): ${invalidLocales.join(', ')}. Must be valid BCP-47 / ISO 639 codes.`);
      }
      return optiFlowApi.createJob(item.id, item.latestVersionId, rawLocaleTokens);
    },
    onSuccess: (res) => {
      setSuccessBanner(
        `Successfully queued ${res.jobs.length} localization job(s) for ${rawLocaleTokens.join(', ')} (Status: 202 Accepted).`
      );
      void queryClient.invalidateQueries({ queryKey: ['jobs'] });
    },
  });

  const handleTogglePreset = (code: string) => {
    const current = new Set(rawLocaleTokens);
    if (current.has(code)) {
      current.delete(code);
    } else {
      current.add(code);
    }
    const next = Array.from(current).join(', ');
    setTargetLocales(next);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Top Job Creation Card */}
      <div className="card" style={{ padding: 22 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12 }}>
          <div>
            <h3 style={{ margin: '0 0 6px 0', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Sparkles size={20} color="#0284c7" />
              <span>Queue New Localization Job (Async 202 Accepted)</span>
            </h3>
            <p style={{ margin: 0, color: '#64748b', fontSize: '0.875rem' }}>
              Select an authoring content item and specify valid target locale(s). OptiFlow will run incremental diffing,
              re-use approved translation memory, and dispatch only modified blocks to the AI translation engine.
            </p>
          </div>
        </div>

        {/* Quick Locale Presets */}
        <div style={{ marginTop: 14, display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#475569', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Globe size={14} /> Quick Presets:
          </span>
          {PRESET_LOCALES.map((preset) => {
            const isSelected = rawLocaleTokens.includes(preset.code);
            return (
              <button
                key={preset.code}
                type="button"
                className={`btn btn-sm ${isSelected ? 'btn-primary' : 'btn-outline'}`}
                style={{ fontSize: '0.78rem', padding: '4px 10px', display: 'inline-flex', alignItems: 'center', gap: 4 }}
                onClick={() => handleTogglePreset(preset.code)}
              >
                <span>{preset.flag}</span>
                <span>{preset.code}</span>
              </button>
            );
          })}
        </div>

        {/* Form Controls */}
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center', marginTop: 14 }}>
          <div style={{ minWidth: 260 }}>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#475569', marginBottom: 4 }}>
              Content Item:
            </label>
            <select
              value={effectiveContentId}
              onChange={(e) => setSelectedContentId(e.target.value)}
              disabled={!itemsQuery.data?.length}
              style={{ width: '100%', fontWeight: 500 }}
            >
              {itemsQuery.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.externalContentKey === 'hotel-home-001'
                    ? '🏠 hotel-home-001 (LandingPage)'
                    : item.externalContentKey === 'hotel-rooms-002'
                    ? '🏨 hotel-rooms-002 (RoomsPage)'
                    : item.externalContentKey === 'hotel-dining-003'
                    ? '🍽️ hotel-dining-003 (DiningAndSpaPage)'
                    : `${item.externalContentKey} (${item.contentType})`}
                </option>
              ))}
            </select>
          </div>

          <div style={{ flex: 1, minWidth: 260 }}>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#475569', marginBottom: 4 }}>
              Target Locales (comma-separated BCP-47 codes):
            </label>
            <input
              value={targetLocales}
              onChange={(e) => {
                setTargetLocales(e.target.value);
                setSuccessBanner(null);
              }}
              placeholder="e.g. es-ES, fr-FR, BD, de-DE"
              style={{
                width: '100%',
                borderColor: hasInvalidLocales ? '#ef4444' : undefined,
                backgroundColor: hasInvalidLocales ? '#fef2f2' : undefined,
              }}
            />
          </div>

          <div style={{ alignSelf: 'flex-end' }}>
            <button
              type="button"
              className="btn btn-primary"
              disabled={!isAuthenticated || !effectiveContentId || createJobMutation.isPending || !isFormValid}
              onClick={() => createJobMutation.mutate()}
              style={{ padding: '9px 20px', fontWeight: 600 }}
            >
              {createJobMutation.isPending ? 'Queuing Job...' : 'Queue Localization'}
            </button>
          </div>
        </div>

        {/* Validation Error Banner */}
        {hasInvalidLocales && (
          <div
            className="alert alert-danger"
            style={{ marginTop: 12, padding: '10px 14px', display: 'flex', alignItems: 'center', gap: 8, fontSize: '0.85rem' }}
          >
            <AlertCircle size={18} color="#b91c1c" style={{ flexShrink: 0 }} />
            <div>
              <strong>Input Validation Blocked:</strong> Invalid locale code{' '}
              <code>&quot;{invalidLocales.join(', ')}&quot;</code>. Target locales must be valid BCP-47 / ISO 639 codes
              (e.g., <code>es-ES</code>, <code>fr-FR</code>, <code>BD</code>, <code>de-DE</code>). Gibberish, profanity,
              and script injection strings are strictly prohibited.
            </div>
          </div>
        )}

        {/* Success Banner */}
        {successBanner && (
          <div
            className="alert alert-success"
            style={{ marginTop: 12, padding: '10px 14px', display: 'flex', alignItems: 'center', gap: 8, fontSize: '0.85rem' }}
          >
            <CheckCircle2 size={18} color="#15803d" style={{ flexShrink: 0 }} />
            <span>{successBanner}</span>
          </div>
        )}

        {/* API Error Banner */}
        {createJobMutation.isError && (
          <div className="alert alert-danger" style={{ marginTop: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <AlertTriangle size={18} />
              <span>{(createJobMutation.error as Error).message}</span>
            </div>
          </div>
        )}
      </div>

      {/* Localization Job Queue Table */}
      <div className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Localization Job Queue</h3>
          <span style={{ fontSize: '0.8rem', color: '#64748b' }}>
            Auto-refreshes every 2.5s while jobs are active
          </span>
        </div>

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
            {jobsQuery.data?.map((job) => {
              const loc = formatLocaleLabel(job.targetLocale);
              return (
                <tr key={job.jobId}>
                  <td className="mono">{job.jobId.slice(0, 8)}...</td>
                  <td>
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontWeight: 600 }}>
                      <span>{loc.flag}</span>
                      <span>{job.targetLocale}</span>
                    </span>
                  </td>
                  <td>
                    <span className={jobBadgeClass(job.status)}>{job.status}</span>
                  </td>
                  <td>
                    {job.attemptCount} / {job.maxAttempts}
                  </td>
                  <td className="mono" style={{ color: job.lastErrorCode ? '#b91c1c' : '#64748b' }}>
                    {job.lastErrorCode ?? '—'}
                  </td>
                  <td>{new Date(job.createdAt).toLocaleTimeString()}</td>
                </tr>
              );
            })}
            {(!jobsQuery.data || jobsQuery.data.length === 0) && (
              <tr>
                <td colSpan={6} style={{ textAlign: 'center', color: '#64748b', padding: '24px' }}>
                  No localization jobs queued yet. Select an item above to queue one.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
