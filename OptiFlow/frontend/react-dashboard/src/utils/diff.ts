export type DiffTokenType = 'added' | 'removed' | 'unchanged' | 'tag';

export interface DiffToken {
  readonly type: DiffTokenType;
  readonly text: string;
}

/**
 * Tokenize a string into words, whitespace, punctuation, and HTML tags.
 */
export function tokenizeText(text: string): string[] {
  if (!text) return [];
  // Matches HTML tags <...> or words/numbers or whitespace sequences or punctuation
  const tokenRegex = /<[^>]+>|[\w\u00C0-\u024F]+|\s+|[^\w\s<>]+/gu;
  const tokens: string[] = [];
  let match: RegExpExecArray | null;

  while ((match = tokenRegex.exec(text)) !== null) {
    tokens.push(match[0]);
  }

  return tokens.length > 0 ? tokens : [text];
}

function isHtmlTag(token: string): boolean {
  return token.startsWith('<') && token.endsWith('>') && token.length >= 3;
}

/**
 * Computes a word-level and tag-aware LCS diff between oldText and newText.
 */
export function computeWordDiff(
  oldText: string | null | undefined,
  newText: string | null | undefined
): DiffToken[] {
  const oldStr = oldText ?? '';
  const newStr = newText ?? '';

  // If there is no previous text (e.g. newly added block), everything in new is added
  if (!oldStr && newStr) {
    return tokenizeText(newStr).map((t) => ({
      type: isHtmlTag(t) ? 'tag' : /^\s+$/.test(t) ? 'unchanged' : 'added',
      text: t,
    }));
  }

  // If new text is empty, everything in old was removed
  if (oldStr && !newStr) {
    return tokenizeText(oldStr).map((t) => ({
      type: isHtmlTag(t) ? 'tag' : /^\s+$/.test(t) ? 'unchanged' : 'removed',
      text: t,
    }));
  }

  // Identical text
  if (oldStr === newStr) {
    return tokenizeText(newStr).map((t) => ({
      type: isHtmlTag(t) ? 'tag' : 'unchanged',
      text: t,
    }));
  }

  const oldTokens = tokenizeText(oldStr);
  const newTokens = tokenizeText(newStr);

  const m = oldTokens.length;
  const n = newTokens.length;

  // Build 2D DP matrix
  const matrix: number[][] = [];
  for (let i = 0; i <= m; i++) {
    matrix[i] = new Array(n + 1).fill(0);
  }

  for (let i = 0; i < m; i++) {
    const rowCurrent = matrix[i] ?? [];
    const rowNext = matrix[i + 1] ?? [];
    for (let j = 0; j < n; j++) {
      if (oldTokens[i] === newTokens[j]) {
        rowNext[j + 1] = (rowCurrent[j] ?? 0) + 1;
      } else {
        const valAbove = rowCurrent[j + 1] ?? 0;
        const valLeft = rowNext[j] ?? 0;
        rowNext[j + 1] = Math.max(valAbove, valLeft);
      }
    }
  }

  // Backtrack to build diff
  const result: DiffToken[] = [];
  let i = m;
  let j = n;

  while (i > 0 || j > 0) {
    const currentRow = matrix[i] ?? [];
    const prevRow = matrix[i - 1] ?? [];
    const oldTok = oldTokens[i - 1] ?? '';
    const newTok = newTokens[j - 1] ?? '';

    if (i > 0 && j > 0 && oldTok === newTok) {
      result.unshift({
        type: isHtmlTag(oldTok) ? 'tag' : 'unchanged',
        text: oldTok,
      });
      i--;
      j--;
    } else if (j > 0 && (i === 0 || (currentRow[j - 1] ?? 0) >= (prevRow[j] ?? 0))) {
      result.unshift({
        type: isHtmlTag(newTok) ? 'tag' : /^\s+$/.test(newTok) ? 'unchanged' : 'added',
        text: newTok,
      });
      j--;
    } else if (i > 0 && (j === 0 || (currentRow[j - 1] ?? 0) < (prevRow[j] ?? 0))) {
      result.unshift({
        type: isHtmlTag(oldTok) ? 'tag' : /^\s+$/.test(oldTok) ? 'unchanged' : 'removed',
        text: oldTok,
      });
      i--;
    } else {
      break;
    }
  }

  return result;
}

/**
 * Format a locale code (e.g. 'de-DE') with its flag or human label.
 */
export function formatLocaleLabel(locale: string): { flag: string; name: string } {
  const normalized = locale.toLowerCase();
  if (normalized.startsWith('de')) return { flag: '🇩🇪', name: 'German (' + locale + ')' };
  if (normalized.startsWith('fr')) return { flag: '🇫🇷', name: 'French (' + locale + ')' };
  if (normalized.startsWith('es')) return { flag: '🇪🇸', name: 'Spanish (' + locale + ')' };
  if (normalized.startsWith('it')) return { flag: '🇮🇹', name: 'Italian (' + locale + ')' };
  if (normalized.startsWith('sv')) return { flag: '🇸🇪', name: 'Swedish (' + locale + ')' };
  if (normalized.startsWith('no')) return { flag: '🇳🇴', name: 'Norwegian (' + locale + ')' };
  if (normalized.startsWith('ja')) return { flag: '🇯🇵', name: 'Japanese (' + locale + ')' };
  if (normalized.startsWith('en')) return { flag: '🇺🇸', name: 'English (' + locale + ')' };
  return { flag: '🌐', name: locale };
}
