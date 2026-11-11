import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Activity, BarChart3, CheckSquare, FileDiff } from 'lucide-react';
import { optiFlowApi, setAuthToken } from './api/client';
import { AnalyticsView } from './components/AnalyticsView';
import { ContentIntelligenceView } from './components/ContentIntelligenceView';
import { OperationsView } from './components/OperationsView';
import { ReviewView } from './components/ReviewView';
import { useLocalizationHub } from './hooks/useLocalizationHub';

type Screen = 'intelligence' | 'operations' | 'review' | 'analytics';

const queryClient = new QueryClient();

function DashboardShell() {
  const [screen, setScreen] = useState<Screen>('intelligence');
  const [token, setToken] = useState<string | null>(null);
  const [tenantId, setTenantId] = useState<string>('11111111-1111-1111-1111-111111111111');
  const [authError, setAuthError] = useState<string | null>(null);

  const hubConnected = useLocalizationHub(token);

  async function handleConnectDevSession() {
    setAuthError(null);
    try {
      const res = await optiFlowApi.mintDevToken(tenantId);
      setAuthToken(res.token);
      setToken(res.token);
      setTenantId(res.tenantId);
      await queryClient.invalidateQueries();
    } catch (err) {
      setAuthError((err as Error).message);
    }
  }

  return (
    <div className="layout">
      <aside className="sidebar">
        <div>
          <div className="brand">OptiFlow</div>
          <div className="brand-sub">Incremental CMS Localization</div>
        </div>

        <button
          type="button"
          className={`nav-btn ${screen === 'intelligence' ? 'active' : ''}`}
          onClick={() => setScreen('intelligence')}
        >
          <FileDiff size={16} /> Content Intelligence
        </button>
        <button
          type="button"
          className={`nav-btn ${screen === 'operations' ? 'active' : ''}`}
          onClick={() => setScreen('operations')}
        >
          <Activity size={16} /> Operations Queue
        </button>
        <button
          type="button"
          className={`nav-btn ${screen === 'review' ? 'active' : ''}`}
          onClick={() => setScreen('review')}
        >
          <CheckSquare size={16} /> Translation Review
        </button>
        <button
          type="button"
          className={`nav-btn ${screen === 'analytics' ? 'active' : ''}`}
          onClick={() => setScreen('analytics')}
        >
          <BarChart3 size={16} /> Cost & Performance
        </button>
      </aside>

      <main className="main">
        <header className="topbar">
          <div>
            <h2 style={{ margin: 0 }}>
              {screen === 'intelligence' && 'Content Intelligence'}
              {screen === 'operations' && 'Localization Operations'}
              {screen === 'review' && 'Translation Review (Draft-Only)'}
              {screen === 'analytics' && 'Cost & Performance Analytics'}
            </h2>
            <span className="mono" style={{ color: '#64748b' }}>
              SignalR: {hubConnected ? 'Connected' : 'Disconnected'}
            </span>
          </div>

          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="mono"
              style={{ width: 290 }}
              aria-label="Tenant ID"
            />
            <button type="button" className="btn btn-primary" onClick={() => void handleConnectDevSession()}>
              {token ? 'Refresh Dev Session' : 'Connect Dev Tenant'}
            </button>
          </div>
        </header>

        {authError && <div className="alert alert-danger">{authError}</div>}

        {!token && (
          <div className="card">
            <p>
              Click <strong>Connect Dev Tenant</strong> (requires <code>OptiFlow.Api</code> running in{' '}
              <code>Development</code>) to authenticate and load tenant-scoped localization data.
            </p>
          </div>
        )}

        {screen === 'intelligence' && <ContentIntelligenceView isAuthenticated={token !== null} />}
        {screen === 'operations' && <OperationsView isAuthenticated={token !== null} />}
        {screen === 'review' && <ReviewView isAuthenticated={token !== null} />}
        {screen === 'analytics' && <AnalyticsView isAuthenticated={token !== null} />}
      </main>
    </div>
  );
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <DashboardShell />
    </QueryClientProvider>
  );
}
