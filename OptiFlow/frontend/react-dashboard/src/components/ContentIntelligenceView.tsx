import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Info, MinusCircle, PlusCircle } from 'lucide-react';
import { optiFlowApi } from '../api/client';
import type { BlockDiffAction } from '../api/types';

function renderDiffBadge(action: BlockDiffAction) {
  switch (action) {
    case 'Unchanged':
      return (
        <span className="badge badge-slate" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <CheckCircle2 size={12} /> Unchanged (0 AI calls)
        </span>
      );
    case 'Modified':
      return (
        <span className="badge badge-amber" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <AlertTriangle size={12} /> Modified (Needs AI)
        </span>
      );
    case 'Added':
      return (
        <span className="badge badge-green" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <PlusCircle size={12} /> Added (Needs AI)
        </span>
      );
    case 'Deleted':
      return (
        <span className="badge badge-red" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <MinusCircle size={12} /> Deleted
        </span>
      );
  }
}

export function ContentIntelligenceView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const queryClient = useQueryClient();
  const [selectedItemId, setSelectedItemId] = useState<string>('');
  const [syncContentKey, setSyncContentKey] = useState('hotel-home-001');
  const [syncLocale, setSyncLocale] = useState('en');

  const connectionsQuery = useQuery({
    queryKey: ['connections'],
    queryFn: optiFlowApi.listConnections,
    enabled: isAuthenticated,
  });

  const itemsQuery = useQuery({
    queryKey: ['content-items'],
    queryFn: optiFlowApi.listContentItems,
    enabled: isAuthenticated,
  });

  const effectiveItemId = selectedItemId || itemsQuery.data?.[0]?.id || '';

  const versionsQuery = useQuery({
    queryKey: ['content-versions', effectiveItemId],
    queryFn: () => optiFlowApi.listVersions(effectiveItemId),
    enabled: isAuthenticated && effectiveItemId.length > 0,
  });

  const diffQuery = useQuery({
    queryKey: ['content-diff', effectiveItemId],
    queryFn: () => optiFlowApi.getDiff(effectiveItemId),
    enabled: isAuthenticated && effectiveItemId.length > 0,
  });

  const [simulationBanner, setSimulationBanner] = useState<string | null>(null);

  const simulateMutation = useMutation({
    mutationFn: (scenario: 'v2' | 'rollback') => optiFlowApi.simulateCmsUpdate(scenario),
    onSuccess: (data) => {
      setSimulationBanner(data.message);
    },
  });

  const syncMutation = useMutation({
    mutationFn: () => {
      const conn = connectionsQuery.data?.[0];
      if (!conn) {
        throw new Error('No CMS connection configured for this tenant.');
      }
      return optiFlowApi.syncContent(conn.id, syncContentKey, syncLocale);
    },
    onSuccess: (res) => {
      setSelectedItemId(res.contentItemId);
      setSimulationBanner(null);
      void queryClient.invalidateQueries({ queryKey: ['content-items'] });
      void queryClient.invalidateQueries({ queryKey: ['content-versions', res.contentItemId] });
      void queryClient.invalidateQueries({ queryKey: ['content-diff', res.contentItemId] });
    },
  });

  return (
    <div>
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12 }}>
          <div>
            <h3 style={{ margin: '0 0 4px 0' }}>Sync Content Snapshot from CMS</h3>
            <p style={{ margin: 0, color: '#64748b', fontSize: '0.875rem' }}>
              Capture an immutable version snapshot of the authoring page from Optimizely CMS.
            </p>
          </div>

          {/* Quick Presets */}
          <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.78rem', color: '#64748b' }}>Presets:</span>
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => {
                setSyncContentKey('hotel-home-001');
                setSyncLocale('en');
              }}
            >
              🏠 hotel-home-001 (en)
            </button>
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => {
                setSyncContentKey('hotel-rooms-002');
                setSyncLocale('en');
              }}
            >
              🏨 hotel-rooms-002 (en)
            </button>
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => {
                setSyncContentKey('hotel-dining-003');
                setSyncLocale('en');
              }}
            >
              🍽️ hotel-dining-003 (en)
            </button>
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => {
                setSyncContentKey('hotel-paris-001');
                setSyncLocale('fr-FR');
              }}
            >
              🇫🇷 hotel-paris-001 (fr-FR)
            </button>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginTop: 14 }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', marginBottom: 2 }}>
              External Content Key:
            </label>
            <input
              value={syncContentKey}
              onChange={(e) => setSyncContentKey(e.target.value)}
              placeholder="e.g. hotel-home-001"
              style={{ width: 220 }}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', marginBottom: 2 }}>
              Source Authoring Locale:
            </label>
            <input
              value={syncLocale}
              onChange={(e) => setSyncLocale(e.target.value)}
              placeholder="e.g. en"
              style={{ width: 140 }}
            />
          </div>

          <div style={{ alignSelf: 'flex-end' }}>
            <button
              type="button"
              className="btn btn-primary"
              disabled={!isAuthenticated || syncMutation.isPending || !connectionsQuery.data?.length}
              onClick={() => syncMutation.mutate()}
            >
              {syncMutation.isPending ? 'Syncing...' : 'Sync Snapshot'}
            </button>
          </div>
        </div>

        <p style={{ marginTop: 8, marginBottom: 0, fontSize: '0.78rem', color: '#64748b' }}>
          💡 <strong>Tip on Locales:</strong> This field specifies the <em>master source language</em> authored in the CMS.
          If your CMS page is written in English (<code>en</code>) and you want French (<code>fr-FR</code>) translations,
          sync with <code>en</code> here, then queue <code>fr-FR</code> as the target in the <strong>Operations Queue</strong> tab.
        </p>

        {syncMutation.isError && (
          <div className="alert alert-danger" style={{ marginTop: 12 }}>
            <strong>Sync Failed:</strong> {(syncMutation.error as Error).message}
            <div style={{ marginTop: 4, fontSize: '0.8rem' }}>
              Verify that the master page exists in the CMS for this exact locale.
            </div>
          </div>
        )}

        {/* CMS Change & Rollback Simulation Section */}
        <div style={{ marginTop: 16, paddingTop: 14, borderTop: '1px solid #f1f5f9' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
            <span style={{ fontSize: '0.82rem', fontWeight: 600, color: '#334155' }}>
              🧪 Test Snapshot Diffing & Rollback (CMS Simulation):
            </span>
            <div style={{ display: 'flex', gap: 8 }}>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                disabled={simulateMutation.isPending}
                onClick={() => simulateMutation.mutate('v2')}
                title="Simulate CMS editor updating hotel-home-001 to version 2"
              >
                ✏️ Simulate CMS Edit (v2)
              </button>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                disabled={simulateMutation.isPending}
                onClick={() => simulateMutation.mutate('rollback')}
                title="Simulate CMS editor rolling back hotel-home-001 to v1 content"
              >
                🔄 Simulate CMS Rollback (v1 as v3)
              </button>
            </div>
          </div>

          {simulationBanner && (
            <div className="alert alert-success" style={{ marginTop: 10, padding: '8px 12px', fontSize: '0.82rem' }}>
              ✓ {simulationBanner}
            </div>
          )}
        </div>
      </div>

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Content Block Diff</h3>
          <select
            value={effectiveItemId}
            onChange={(e) => setSelectedItemId(e.target.value)}
            disabled={!itemsQuery.data?.length}
          >
            {itemsQuery.data?.map((item) => (
              <option key={item.id} value={item.id}>
                {item.externalContentKey} ({item.contentType})
              </option>
            ))}
          </select>
        </div>

        {diffQuery.data ? (
          <>
            <div className="grid-4">
              <div className="card">
                <div className="kpi-label">Total Blocks</div>
                <div className="kpi-value">{diffQuery.data.totalBlocks}</div>
              </div>
              <div className="card">
                <div className="kpi-label">Unchanged (0 AI calls)</div>
                <div className="kpi-value">{diffQuery.data.unchangedCount}</div>
              </div>
              <div className="card">
                <div className="kpi-label">Modified / Added</div>
                <div className="kpi-value">{diffQuery.data.modifiedCount + diffQuery.data.addedCount}</div>
              </div>
              <div className="card">
                <div className="kpi-label">Deleted</div>
                <div className="kpi-value">{diffQuery.data.deletedCount}</div>
              </div>
            </div>

            <div className="alert alert-success" style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
              <Info size={18} />
              <span>
                <strong>Incremental Diff Active:</strong> Only modified and added blocks trigger AI translation or Translation Memory lookups. Unchanged blocks are automatically preserved at zero AI token cost.
              </span>
            </div>

            <table className="table">
              <thead>
                <tr>
                  <th>Ordinal</th>
                  <th>Block Key</th>
                  <th>Field Path</th>
                  <th>Type</th>
                  <th>Status</th>
                  <th>SHA-256 Hash</th>
                </tr>
              </thead>
              <tbody>
                {diffQuery.data.blocks.map((block) => (
                  <tr key={`${block.blockKey}-${block.ordinal}`}>
                    <td>{block.ordinal}</td>
                    <td className="mono">{block.blockKey}</td>
                    <td className="mono">{block.fieldPath}</td>
                    <td>{block.blockType}</td>
                    <td>{renderDiffBadge(block.action)}</td>
                    <td className="mono">{block.contentHash.slice(0, 16)}...</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        ) : (
          <p>Sync a content item to inspect immutable snapshots and block-level SHA-256 diffs.</p>
        )}
      </div>

      {versionsQuery.data && versionsQuery.data.length > 0 && (
        <div className="card">
          <h3>Immutable Snapshot History</h3>
          <table className="table">
            <thead>
              <tr>
                <th>Version #</th>
                <th>External ETag / Key</th>
                <th>Locale</th>
                <th>Blocks</th>
                <th>Snapshot Hash</th>
                <th>Captured At</th>
              </tr>
            </thead>
            <tbody>
              {versionsQuery.data.map((v) => (
                <tr key={v.id}>
                  <td>v{v.versionNumber}</td>
                  <td className="mono">{v.externalVersionKey}</td>
                  <td>{v.locale}</td>
                  <td>{v.blockCount}</td>
                  <td className="mono">{v.contentHash.slice(0, 16)}...</td>
                  <td>{new Date(v.capturedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
