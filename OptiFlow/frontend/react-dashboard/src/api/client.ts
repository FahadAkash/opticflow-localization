import type {
  ApiError,
  ConnectionResponse,
  ContentItemSummary,
  ContentVersionResponse,
  CostAnalyticsResponse,
  DevTokenResponse,
  DiffResponse,
  JobBlockResponse,
  JobStatusResponse,
  PerformanceAnalyticsResponse,
  ReviewBlockOverride,
  ReviewDecisionResponse,
  ReviewTaskResponse,
  SyncContentResponse,
} from './types';

export class ApiRequestError extends Error {
  public readonly status: number;
  public readonly code: string;

  constructor(status: number, error: ApiError) {
    super(error.message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.code = error.code;
  }
}

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '';

let authToken: string | null = null;

export function setAuthToken(token: string | null): void {
  authToken = token;
}

export function getAuthToken(): string | null {
  return authToken;
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  if (!headers.has('Content-Type') && init?.body) {
    headers.set('Content-Type', 'application/json');
  }
  if (authToken) {
    headers.set('Authorization', `Bearer ${authToken}`);
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    let parsedError: ApiError = {
      message: `HTTP ${response.status}`,
      code: 'HTTP_ERROR',
    };
    try {
      const json = (await response.json()) as Partial<ApiError>;
      if (typeof json.message === 'string' && typeof json.code === 'string') {
        parsedError = { message: json.message, code: json.code };
      }
    } catch {
      // Leave default normalized error
    }
    throw new ApiRequestError(response.status, parsedError);
  }

  return (await response.json()) as T;
}

export const optiFlowApi = {
  mintDevToken: (tenantId: string, actorId = 'dashboard-reviewer'): Promise<DevTokenResponse> =>
    requestJson<DevTokenResponse>('/api/v1/dev/token', {
      method: 'POST',
      body: JSON.stringify({ tenantId, actorId }),
    }),

  listConnections: (): Promise<readonly ConnectionResponse[]> =>
    requestJson<readonly ConnectionResponse[]>('/api/v1/cms/connections'),

  listContentItems: (): Promise<readonly ContentItemSummary[]> =>
    requestJson<readonly ContentItemSummary[]>('/api/v1/content'),

  syncContent: (
    connectionId: string,
    externalContentKey: string,
    locale = 'en'
  ): Promise<SyncContentResponse> =>
    requestJson<SyncContentResponse>('/api/v1/content/sync', {
      method: 'POST',
      body: JSON.stringify({ connectionId, externalContentKey, locale }),
    }),

  listVersions: (contentId: string): Promise<readonly ContentVersionResponse[]> =>
    requestJson<readonly ContentVersionResponse[]>(`/api/v1/content/${contentId}/versions`),

  getDiff: (contentId: string, fromVersionId?: string, toVersionId?: string): Promise<DiffResponse> => {
    const params = new URLSearchParams();
    if (fromVersionId) params.set('fromVersionId', fromVersionId);
    if (toVersionId) params.set('toVersionId', toVersionId);
    const qs = params.toString();
    return requestJson<DiffResponse>(`/api/v1/content/${contentId}/diff${qs ? `?${qs}` : ''}`);
  },

  listJobs: (): Promise<readonly JobStatusResponse[]> =>
    requestJson<readonly JobStatusResponse[]>('/api/v1/localization/jobs'),

  createJob: (
    contentId: string,
    sourceVersionId: string,
    targetLocales: readonly string[],
    idempotencyKey?: string
  ): Promise<{ readonly jobs: readonly JobStatusResponse[] }> =>
    requestJson<{ readonly jobs: readonly JobStatusResponse[] }>('/api/v1/localization/jobs', {
      method: 'POST',
      body: JSON.stringify({ contentId, sourceVersionId, targetLocales, idempotencyKey }),
    }),

  getJobBlocks: (jobId: string): Promise<readonly JobBlockResponse[]> =>
    requestJson<readonly JobBlockResponse[]>(`/api/v1/localization/jobs/${jobId}/blocks`),

  listPendingReviews: (): Promise<readonly ReviewTaskResponse[]> =>
    requestJson<readonly ReviewTaskResponse[]>('/api/v1/reviews'),

  approveReview: (
    reviewId: string,
    comments?: string,
    blockOverrides?: readonly ReviewBlockOverride[]
  ): Promise<ReviewDecisionResponse> =>
    requestJson<ReviewDecisionResponse>(`/api/v1/reviews/${reviewId}/approve`, {
      method: 'POST',
      body: JSON.stringify({ comments, blockOverrides }),
    }),

  rejectReview: (reviewId: string, comments?: string): Promise<ReviewDecisionResponse> =>
    requestJson<ReviewDecisionResponse>(`/api/v1/reviews/${reviewId}/reject`, {
      method: 'POST',
      body: JSON.stringify({ comments }),
    }),

  getCostAnalytics: (): Promise<CostAnalyticsResponse> =>
    requestJson<CostAnalyticsResponse>('/api/v1/analytics/costs'),

  getPerformanceAnalytics: (): Promise<PerformanceAnalyticsResponse> =>
    requestJson<PerformanceAnalyticsResponse>('/api/v1/analytics/performance'),
};
