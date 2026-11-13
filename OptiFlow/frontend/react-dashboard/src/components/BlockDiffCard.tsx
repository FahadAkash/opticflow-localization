import { useState } from 'react';
import { AlertTriangle, Bot, Copy, Edit3, RotateCcw, ShieldCheck, Zap } from 'lucide-react';
import type { BlockDiffAction, BlockResolutionAction, JobBlockResponse, ParsedValidationBreakdown } from '../api/types';
import { computeWordDiff, formatLocaleLabel, type DiffToken } from '../utils/diff';
import { QualityCheckBadge } from './QualityCheckBadge';

interface BlockDiffCardProps {
  readonly block: JobBlockResponse;
  readonly targetLocale: string;
  readonly validation: ParsedValidationBreakdown | null;
  readonly overrideValue: string;
  readonly onOverrideChange: (blockKey: string, value: string) => void;
}

function renderDiffTokens(tokens: DiffToken[]) {
  return (
    <span>
      {tokens.map((token, index) => {
        if (token.type === 'tag') {
          return (
            <span key={index} className="diff-tag" title="HTML Tag">
              {token.text}
            </span>
          );
        }
        if (token.type === 'added') {
          return (
            <ins key={index} className="diff-ins">
              {token.text}
            </ins>
          );
        }
        if (token.type === 'removed') {
          return (
            <del key={index} className="diff-del">
              {token.text}
            </del>
          );
        }
        return <span key={index}>{token.text}</span>;
      })}
    </span>
  );
}

function getResolutionMeta(action: BlockResolutionAction): {
  icon: JSX.Element;
  label: string;
  className: string;
  description: string;
} {
  switch (action) {
    case 'AiTranslated':
      return {
        icon: <Bot size={13} />,
        label: 'AI Translated',
        className: 'badge badge-blue',
        description: 'New or modified text translated by Gemma 4B',
      };
    case 'ReusedTranslationMemory':
      return {
        icon: <Zap size={13} />,
        label: '100% TM Reuse',
        className: 'badge badge-green',
        description: 'Exact match from approved Translation Memory ($0 AI cost)',
      };
    case 'ReusedUnchanged':
      return {
        icon: <ShieldCheck size={13} />,
        label: 'Unchanged Source',
        className: 'badge badge-slate',
        description: 'Source text identical to previous version; translation preserved',
      };
    case 'PreservedHumanEdit':
      return {
        icon: <ShieldCheck size={13} />,
        label: 'Human Edit Preserved',
        className: 'badge badge-purple',
        description: 'Previously approved human translation protected from AI overwrite',
      };
    case 'ConflictRequiresReview':
      return {
        icon: <AlertTriangle size={13} />,
        label: 'Review Conflict',
        className: 'badge badge-amber',
        description: 'Conflict between source change and human-edited translation',
      };
    case 'ProposedRemoval':
      return {
        icon: <AlertTriangle size={13} />,
        label: 'Proposed Removal',
        className: 'badge badge-red',
        description: 'Content block was deleted from master source',
      };
  }
}

function getDiffActionBadge(action: BlockDiffAction): { label: string; className: string } {
  switch (action) {
    case 'Added':
      return { label: 'New Block in v1', className: 'badge badge-green' };
    case 'Modified':
      return { label: 'Source Modified', className: 'badge badge-amber' };
    case 'Unchanged':
      return { label: 'Unchanged', className: 'badge badge-slate' };
    case 'Deleted':
      return { label: 'Deleted', className: 'badge badge-red' };
  }
}

export function BlockDiffCard({
  block,
  targetLocale,
  validation,
  overrideValue,
  onOverrideChange,
}: BlockDiffCardProps) {
  const [isEditing, setIsEditing] = useState(overrideValue.trim().length > 0);
  const targetInfo = formatLocaleLabel(targetLocale);
  const resolutionMeta = getResolutionMeta(block.resolutionAction);
  const diffActionBadge = getDiffActionBadge(block.actionType);

  const hasPrevious = Boolean(block.previousTranslatedText && block.previousTranslatedText.trim().length > 0);
  const isNewlyAdded = block.actionType === 'Added' || !hasPrevious;

  // Compute word diff between previous translation and proposed translation
  const translationDiff = hasPrevious
    ? computeWordDiff(block.previousTranslatedText, block.translatedText)
    : computeWordDiff(null, block.translatedText);

  // Compute word tokens for source text (safely tokenizes tags)
  const sourceTokens = computeWordDiff(null, block.sourceText);

  function handleCopyProposed() {
    if (block.translatedText) {
      onOverrideChange(block.blockKey, block.translatedText);
      setIsEditing(true);
    }
  }

  return (
    <div
      className={`card block-review-card ${
        block.hasHumanEditConflict ? 'border-conflict' : block.actionType === 'Modified' ? 'border-modified' : ''
      }`}
    >
      {/* Block Header */}
      <div className="block-card-header">
        <div>
          <span className="block-title mono">
            <strong>{block.blockKey}</strong>
          </span>
          <span className="block-path mono">({block.fieldPath})</span>
          <span className="block-type-pill">{block.blockType}</span>
        </div>

        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
          <span className={diffActionBadge.className}>{diffActionBadge.label}</span>
          <span
            className={resolutionMeta.className}
            title={resolutionMeta.description}
            style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
          >
            {resolutionMeta.icon}
            {resolutionMeta.label}
          </span>
          {block.hasHumanEditConflict && (
            <span
              className={`badge ${overrideValue ? 'badge-green' : 'badge-red'}`}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
            >
              <AlertTriangle size={12} />
              {overrideValue ? 'CONFLICT RESOLVED' : 'HUMAN EDIT CONFLICT'}
            </span>
          )}
          {block.status === 'VALIDATION_FAILED' && (
            <span
              className={`badge ${overrideValue ? 'badge-green' : 'badge-red'}`}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
            >
              <AlertTriangle size={12} />
              {overrideValue ? 'VALIDATION OVERRIDDEN' : 'VALIDATION FAILED'}
            </span>
          )}
        </div>
      </div>

      {/* Human Conflict Banner */}
      {block.hasHumanEditConflict && (
        <div
          className={`alert ${overrideValue ? 'alert-success' : 'alert-danger'}`}
          style={{ marginBottom: 12, padding: '10px 14px' }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10 }}>
            <div>
              <strong>{overrideValue ? '✅ Conflict Resolved by Reviewer:' : '⚠️ Reviewer Decision Required:'}</strong>{' '}
              {overrideValue
                ? `Using custom reviewer resolution text. This will be promoted to HUMAN_APPROVED TM upon draft approval.`
                : `Master source changed, but a human translator previously approved an edit for this block. Choose an option to resolve:`}
            </div>

            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              <button
                type="button"
                className="btn btn-sm btn-primary"
                onClick={() => {
                  if (block.translatedText) {
                    onOverrideChange(block.blockKey, block.translatedText);
                    setIsEditing(true);
                  }
                }}
                style={{ fontSize: '0.78rem' }}
              >
                ⚡ Accept Proposed AI
              </button>

              {block.previousTranslatedText && (
                <button
                  type="button"
                  className="btn btn-sm btn-secondary"
                  onClick={() => {
                    onOverrideChange(block.blockKey, block.previousTranslatedText!);
                    setIsEditing(true);
                  }}
                  style={{ fontSize: '0.78rem' }}
                >
                  🛡️ Keep Previous Human Edit
                </button>
              )}

              <button
                type="button"
                className="btn btn-sm btn-outline"
                onClick={() => setIsEditing(true)}
                style={{ fontSize: '0.78rem' }}
              >
                ✏️ Custom Edit
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Validation Failed Banner */}
      {block.status === 'VALIDATION_FAILED' && (
        <div
          className={`alert ${overrideValue ? 'alert-success' : 'alert-danger'}`}
          style={{ marginBottom: 12, padding: '10px 14px' }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10 }}>
            <div>
              <strong>{overrideValue ? '✅ Validation Resolved by Reviewer:' : '❌ Quality Check Failed:'}</strong>{' '}
              {overrideValue
                ? `Reviewer provided valid approved text to resolve this failure.`
                : `This translation failed one or more automated quality gates. Under OptiFlow rules, you must edit or override this block before the draft can be approved.`}
            </div>
            {!overrideValue && (
              <button
                type="button"
                className="btn btn-sm btn-primary"
                onClick={() => {
                  onOverrideChange(block.blockKey, block.translatedText || block.sourceText);
                  setIsEditing(true);
                }}
                style={{ fontSize: '0.78rem' }}
              >
                ✏️ Edit & Fix Translation
              </button>
            )}
          </div>
        </div>
      )}

      {/* Adaptive Diff Panes */}
      {isNewlyAdded ? (
        // 2-Column Clean Layout for New Additions
        <div className="diff-grid-2col">
          <div className="diff-column">
            <div className="diff-column-header">
              <span className="kpi-label">🇺🇸 Master Source (English)</span>
              <span className="mono" style={{ fontSize: '0.72rem', color: '#94a3b8' }}>
                SHA: {block.sourceHash.slice(0, 8)}
              </span>
            </div>
            <div className="diff-pane source-pane">{renderDiffTokens(sourceTokens)}</div>
          </div>

          <div className="diff-column">
            <div className="diff-column-header">
              <span className="kpi-label">
                {targetInfo.flag} Proposed Translation ({targetInfo.name})
              </span>
              <span className="badge badge-blue" style={{ fontSize: '0.7rem' }}>
                {resolutionMeta.label}
              </span>
            </div>
            <div className="diff-pane target-pane new-content-pane">
              {block.translatedText ? renderDiffTokens(translationDiff) : <span style={{ color: '#94a3b8' }}>—</span>}
            </div>
          </div>
        </div>
      ) : (
        // 3-Column Side-by-Side Diff for Modified Content
        <div className="diff-grid-3col">
          <div className="diff-column">
            <div className="diff-column-header">
              <span className="kpi-label">🇺🇸 Master Source (Current)</span>
            </div>
            <div className="diff-pane source-pane">{renderDiffTokens(sourceTokens)}</div>
          </div>

          <div className="diff-column">
            <div className="diff-column-header">
              <span className="kpi-label">{targetInfo.flag} Previous Published Translation</span>
            </div>
            <div className="diff-pane previous-pane">{block.previousTranslatedText ?? '—'}</div>
          </div>

          <div className="diff-column">
            <div className="diff-column-header">
              <span className="kpi-label">
                {targetInfo.flag} Proposed Translation (Word Diff)
              </span>
              <span className="badge badge-amber" style={{ fontSize: '0.7rem' }}>
                Modified
              </span>
            </div>
            <div className="diff-pane target-pane diff-active-pane">
              {block.translatedText ? renderDiffTokens(translationDiff) : <span style={{ color: '#94a3b8' }}>—</span>}
            </div>
          </div>
        </div>
      )}

      {/* Automated Quality Gate */}
      {validation && <QualityCheckBadge validation={validation} />}

      {/* Reviewer Override / Conflict Resolution Box */}
      <div className="override-section" style={{ marginTop: 12 }}>
        {!isEditing && !overrideValue ? (
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={() => setIsEditing(true)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: '0.8rem' }}
          >
            <Edit3 size={13} /> Edit or override translation
          </button>
        ) : (
          <div className="override-box">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
              <label style={{ fontSize: '0.82rem', fontWeight: 600, color: '#334155' }}>
                ✏️ Reviewer Override for {block.blockKey}
              </label>
              <div style={{ display: 'flex', gap: 6 }}>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  onClick={handleCopyProposed}
                  title="Copy proposed AI translation into editor"
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: '0.75rem' }}
                >
                  <Copy size={11} /> Copy Proposed Text
                </button>
                {overrideValue && (
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={() => onOverrideChange(block.blockKey, '')}
                    title="Clear override"
                    style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: '0.75rem' }}
                  >
                    <RotateCcw size={11} /> Reset
                  </button>
                )}
              </div>
            </div>

            <input
              style={{ width: '100%', padding: '8px 10px' }}
              value={overrideValue}
              onChange={(e) => onOverrideChange(block.blockKey, e.target.value)}
              placeholder="Enter approved custom translation..."
            />

            <div style={{ marginTop: 4, fontSize: '0.75rem', color: '#64748b' }}>
              💡 Your manual edit will overwrite the AI output in the CMS draft and will be promoted to Translation
              Memory as <strong style={{ color: '#166534' }}>HUMAN_APPROVED</strong> for future reuse.
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
