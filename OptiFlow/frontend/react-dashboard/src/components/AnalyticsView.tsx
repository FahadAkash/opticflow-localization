import { useQuery } from '@tanstack/react-query';
import { optiFlowApi } from '../api/client';

export function AnalyticsView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const costsQuery = useQuery({
    queryKey: ['analytics', 'costs'],
    queryFn: optiFlowApi.getCostAnalytics,
    enabled: isAuthenticated,
  });

  const perfQuery = useQuery({
    queryKey: ['analytics', 'performance'],
    queryFn: optiFlowApi.getPerformanceAnalytics,
    enabled: isAuthenticated,
  });

  const costs = costsQuery.data;
  const perf = perfQuery.data;

  return (
    <div>
      <div className="grid-4">
        <div className="card">
          <div className="kpi-label">Translation Reuse Rate</div>
          <div className="kpi-value">{costs ? `${costs.translationReuseRate}%` : '—'}</div>
        </div>
        <div className="card">
          <div className="kpi-label">AI Credit Reduction vs Baseline</div>
          <div className="kpi-value">{costs ? `${costs.aiCreditReductionPercent}%` : '—'}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Content Preservation Rate</div>
          <div className="kpi-value">{costs ? `${costs.contentPreservationRate}%` : '—'}</div>
        </div>
        <div className="card">
          <div className="kpi-label">Job Success Rate</div>
          <div className="kpi-value">{perf ? `${perf.jobSuccessRate}%` : '—'}</div>
        </div>
      </div>

      <div className="card">
        <h3>Token & Cost Comparison (Local Gemma vs Cloud Baseline)</h3>
        {costs ? (
          <table className="table">
            <tbody>
              <tr>
                <th>Total Jobs Processed</th>
                <td>{costs.totalJobs}</td>
              </tr>
              <tr>
                <th>Total Blocks / Reused or Preserved</th>
                <td>
                  {costs.totalBlocksProcessed} / {costs.blocksReusedOrPreserved}
                </td>
              </tr>
              <tr>
                <th>Incremental AI Tokens (Input / Output)</th>
                <td>
                  {costs.incrementalInputTokens} / {costs.incrementalOutputTokens}
                </td>
              </tr>
              <tr>
                <th>Estimated Full-Page Baseline Tokens</th>
                <td>{costs.estimatedBaselineTokens}</td>
              </tr>
              <tr>
                <th>Local LLM API Token Fee</th>
                <td>${costs.estimatedLocalCostUsd.toFixed(4)} USD (electricity/GPU excluded)</td>
              </tr>
              <tr>
                <th>Estimated Cloud Equivalent Cost</th>
                <td>${costs.estimatedCloudEquivalentCostUsd.toFixed(6)} USD</td>
              </tr>
            </tbody>
          </table>
        ) : (
          <p>Loading cost metrics...</p>
        )}
      </div>

      <div className="card">
        <h3>AI Inference Latency Percentiles</h3>
        {perf ? (
          <div className="grid-4">
            <div className="card">
              <div className="kpi-label">p50 Latency</div>
              <div className="kpi-value">{perf.latencyP50Ms} ms</div>
            </div>
            <div className="card">
              <div className="kpi-label">p95 Latency</div>
              <div className="kpi-value">{perf.latencyP95Ms} ms</div>
            </div>
            <div className="card">
              <div className="kpi-label">p99 Latency</div>
              <div className="kpi-value">{perf.latencyP99Ms} ms</div>
            </div>
            <div className="card">
              <div className="kpi-label">Failed / Dead-Letter</div>
              <div className="kpi-value">{perf.failedOrDeadLetterJobs}</div>
            </div>
          </div>
        ) : (
          <p>Loading latency metrics...</p>
        )}
      </div>
    </div>
  );
}
