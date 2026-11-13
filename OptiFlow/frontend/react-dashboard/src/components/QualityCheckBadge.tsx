import { useState } from 'react';
import { CheckCircle2, ChevronDown, ChevronUp, ShieldAlert } from 'lucide-react';
import type { ParsedValidationBreakdown } from '../api/types';

interface QualityCheckBadgeProps {
  readonly validation: ParsedValidationBreakdown;
}

export function QualityCheckBadge({ validation }: QualityCheckBadgeProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  const checks = validation.checks ?? [];
  const passedCount = checks.filter((c) => c.passed).length;
  const totalCount = checks.length;
  const allPassed = totalCount > 0 && passedCount === totalCount;
  const failedChecks = checks.filter((c) => !c.passed);

  if (totalCount === 0 && !validation.failureSummary) {
    return null;
  }

  return (
    <div className="quality-check-container" style={{ marginTop: 10 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        {allPassed ? (
          <button
            type="button"
            className="quality-pill quality-pill-pass"
            onClick={() => setIsExpanded((prev) => !prev)}
            title="Click to view automated quality check breakdown"
          >
            <CheckCircle2 size={14} />
            <span>All {totalCount} Automated Checks Passed</span>
            {isExpanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          </button>
        ) : (
          <button
            type="button"
            className="quality-pill quality-pill-fail"
            onClick={() => setIsExpanded((prev) => !prev)}
          >
            <ShieldAlert size={14} />
            <span>
              {failedChecks.length} of {totalCount} Checks Failed
            </span>
            {isExpanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          </button>
        )}

        <span style={{ fontSize: '0.78rem', color: '#64748b' }}>
          Deterministic gates: JSON, length, HTML tags, glossary, placeholders, and source version
        </span>
      </div>

      {validation.failureSummary && (
        <div className="alert alert-danger" style={{ marginTop: 8, padding: '8px 12px', fontSize: '0.85rem' }}>
          <strong>Validation Alert:</strong> {validation.failureSummary}
        </div>
      )}

      {isExpanded && (
        <div className="quality-dropdown-card">
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
            {checks.map((chk) => (
              <span
                key={chk.checkName}
                className={chk.passed ? 'badge badge-green' : 'badge badge-red'}
                title={chk.detail ?? undefined}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
              >
                {chk.passed ? <CheckCircle2 size={11} /> : <ShieldAlert size={11} />}
                <strong>{chk.checkName}</strong>: {chk.passed ? 'PASS' : 'FAIL'}
                {chk.detail && <span style={{ opacity: 0.85 }}>({chk.detail})</span>}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
