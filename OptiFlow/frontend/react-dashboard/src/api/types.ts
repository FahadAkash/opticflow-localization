export type JobStatus =
  | 'Queued'
  | 'Snapshotting'
  | 'Diffing'
  | 'Translating'
  | 'Validating'
  | 'RetryWait'
  | 'NeedsManualReview'
  | 'AwaitingReview'
  | 'Approved'
  | 'Rejected'
  | 'CmsDraftCreated'
  | 'StaleConflict'
  | 'Failed'
  | 'DeadLetter';

export type BlockDiffAction = 'Unchanged' | 'Modified' | 'Added' | 'Deleted';

export type BlockResolutionAction =
  | 'ReusedUnchanged'
  | 'PreservedHumanEdit'
  | 'ReusedTranslationMemory'
  | 'AiTranslated'
  | 'ConflictRequiresReview'
  | 'ProposedRemoval';

export type ReviewStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ApiError {
  readonly message: string;
  readonly code: string;
}

export interface DevTokenResponse {
  readonly token: string;
  readonly tenantId: string;
}

export interface ConnectionResponse {
  readonly id: string;
  readonly provider: string;
  readonly baseUrl: string;
  readonly status: string;
  readonly createdAt: string;
}

export interface ContentItemSummary {
  readonly id: string;
  readonly connectionId: string;
  readonly externalContentKey: string;
  readonly contentType: string;
  readonly sourceLocale: string;
  readonly latestVersionId: string | null;
  readonly createdAt: string;
}

export interface ContentVersionResponse {
  readonly id: string;
  readonly externalVersionKey: string;
  readonly locale: string;
  readonly versionNumber: number;
  readonly contentHash: string;
  readonly blockCount: number;
  readonly capturedAt: string;
}

export interface DiffBlockDto {
  readonly blockKey: string;
  readonly fieldPath: string;
  readonly blockType: string;
  readonly contentHash: string;
  readonly action: BlockDiffAction;
  readonly ordinal: number;
}

export interface DiffResponse {
  readonly contentItemId: string;
  readonly previousVersionId: string | null;
  readonly currentVersionId: string;
  readonly totalBlocks: number;
  readonly unchangedCount: number;
  readonly modifiedCount: number;
  readonly addedCount: number;
  readonly deletedCount: number;
  readonly blocks: readonly DiffBlockDto[];
}

export interface ContentPreviewBlock {
  readonly blockKey: string;
  readonly fieldPath: string;
  readonly blockType: string;
  readonly sourceText: string;
  readonly displayText: string;
  readonly provenance: string;
  readonly ordinal: number;
}

export interface ContentPreviewResponse {
  readonly contentItemId: string;
  readonly externalContentKey: string;
  readonly contentType: string;
  readonly sourceLocale: string;
  readonly activeLocale: string;
  readonly availableLocales: readonly string[];
  readonly blocks: readonly ContentPreviewBlock[];
}

export interface SyncContentResponse {
  readonly contentItemId: string;
  readonly versionId: string;
  readonly externalContentKey: string;
  readonly externalVersionKey: string;
  readonly versionNumber: number;
  readonly contentHash: string;
  readonly blockCount: number;
  readonly isNewVersion: boolean;
}

export interface JobStatusResponse {
  readonly jobId: string;
  readonly contentItemId: string;
  readonly sourceVersionId: string;
  readonly targetLocale: string;
  readonly status: JobStatus;
  readonly attemptCount: number;
  readonly maxAttempts: number;
  readonly lastErrorCode: string | null;
  readonly createdAt: string;
  readonly completedAt: string | null;
}

export interface JobBlockResponse {
  readonly id: string;
  readonly blockKey: string;
  readonly fieldPath: string;
  readonly blockType: string;
  readonly sourceText: string;
  readonly sourceHash: string;
  readonly actionType: BlockDiffAction;
  readonly resolutionAction: BlockResolutionAction;
  readonly status: string;
  readonly previousTranslatedText: string | null;
  readonly translatedText: string | null;
  readonly translationMemoryId: string | null;
  readonly hasHumanEditConflict: boolean;
  readonly validationResult: string | null;
}

export interface ReviewTaskResponse {
  readonly reviewId: string;
  readonly jobId: string;
  readonly contentItemId: string;
  readonly targetLocale: string;
  readonly status: ReviewStatus;
  readonly jobStatus: JobStatus;
  readonly expectedCmsVersion: string | null;
  readonly cmsDraftId: string | null;
  readonly createdAt: string;
  readonly reviewedAt: string | null;
}

export interface ReviewBlockOverride {
  readonly blockKey: string;
  readonly approvedText: string;
}

export interface ReviewDecisionResponse {
  readonly reviewId: string;
  readonly jobId: string;
  readonly jobStatus: JobStatus;
  readonly cmsDraftId: string | null;
  readonly newCmsVersion: string | null;
}

export interface CostAnalyticsResponse {
  readonly totalJobs: number;
  readonly totalBlocksProcessed: number;
  readonly blocksReusedOrPreserved: number;
  readonly translationReuseRate: number;
  readonly contentPreservationRate: number;
  readonly incrementalInputTokens: number;
  readonly incrementalOutputTokens: number;
  readonly estimatedBaselineTokens: number;
  readonly aiCreditReductionPercent: number;
  readonly estimatedLocalCostUsd: number;
  readonly estimatedCloudEquivalentCostUsd: number;
}

export interface PerformanceAnalyticsResponse {
  readonly totalJobs: number;
  readonly succeededJobs: number;
  readonly failedOrDeadLetterJobs: number;
  readonly jobSuccessRate: number;
  readonly latencyP50Ms: number;
  readonly latencyP95Ms: number;
  readonly latencyP99Ms: number;
}

export interface DeterministicCheckResult {
  readonly checkName: string;
  readonly passed: boolean;
  readonly detail?: string | null;
}

export interface ParsedValidationBreakdown {
  readonly isValid: boolean;
  readonly checks: readonly DeterministicCheckResult[];
  readonly failureSummary?: string | null;
}
