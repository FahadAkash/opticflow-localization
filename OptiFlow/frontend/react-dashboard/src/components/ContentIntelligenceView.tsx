import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { optiFlowApi } from '../api/client';
import type { BlockDiffAction } from '../api/types';

function diffBadgeClass(action: BlockDiffAction): string {
  switch (action) {
    case 'Unchanged':
      return 'badge badge-slate';
    case 'Modified':
      return 'badge badge-amber';
    case 'Added':
      return 'badge badge-green';
    case 'Deleted':
      return 'badge badge-red';
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
      void queryClient.invalidateQueries({ queryKey: ['content-items'] });
      void queryClient.invalidateQueries({ queryKey: ['content-versions', res.contentItemId] });
      void queryClient.invalidateQueries({ queryKey: ['content-diff', res.contentItemId] });
    },
  });

  return (
    <div>
      <div className="card">
        <h3>Sync Content Snapshot</h3>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
          <input
            value={syncContentKey}
            onChange={(e) => setSyncContentKey(e.target.value)}
            placeholder="External content key"
          />
          <input
            value={syncLocale}
            onChange={(e) => setSyncLocale(e.target.value)}
            placeholder="Source locale (e.g. en)"
            style={{ width: 120 }}
          />
          <button
            type="button"
            className="btn btn-primary"
            disabled={!isAuthenticated || syncMutation.isPending || !connectionsQuery.data?.length}
            onClick={() => syncMutation.mutate()}
          >
            {syncMutation.isPending ? 'Syncing...' : 'Sync Snapshot'}
          </button>
        </div>
        {syncMutation.isError && (
          <div className="alert alert-danger" style={{ marginTop: 12 }}>
            {(syncMutation.error as Error).message}
          </div>
        )}
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
                    <td>
                      <span className={diffBadgeClass(block.action)}>{block.action}</span>
                    </td>
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
