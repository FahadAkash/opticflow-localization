using Microsoft.EntityFrameworkCore;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Infrastructure.Persistence;

public sealed class PostgresJobQueue : IJobQueue
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim FallbackLock = new(1, 1);

    private readonly OptiFlowDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PostgresJobQueue(OptiFlowDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<LocalizationJob?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (_db.Database.IsNpgsql())
        {
            DateTimeOffset leaseExpiresAt = now.Add(LeaseDuration);
            return await _db.LocalizationJobs
                .FromSqlRaw(
                    """
                    WITH next_job AS (
                        SELECT id
                        FROM localization_jobs
                        WHERE (
                                (status IN ('Queued', 'RetryWait', 'StaleConflict') AND available_at <= {0})
                             OR (status IN ('Snapshotting', 'Diffing', 'Translating', 'Validating') AND (lease_expires_at IS NULL OR lease_expires_at < {0}))
                              )
                          AND attempt_count < max_attempts
                        ORDER BY priority DESC, created_at ASC
                        FOR UPDATE SKIP LOCKED
                        LIMIT 1
                    )
                    UPDATE localization_jobs j
                    SET status           = CASE WHEN j.status = 'StaleConflict' THEN 'Diffing' ELSE 'Snapshotting' END,
                        locked_by        = {1},
                        locked_at        = {0},
                        lease_expires_at = {2},
                        attempt_count    = j.attempt_count + 1
                    FROM next_job
                    WHERE j.id = next_job.id
                    RETURNING j.*;
                    """,
                    now,
                    workerId,
                    leaseExpiresAt)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // Portable fallback for offline SQLite integration tests
        await FallbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<LocalizationJob> candidates = await _db.LocalizationJobs
                .IgnoreQueryFilters()
                .Where(j => j.AttemptCount < j.MaxAttempts &&
                            (j.Status == JobStatus.Queued ||
                             j.Status == JobStatus.RetryWait ||
                             j.Status == JobStatus.StaleConflict ||
                             j.Status == JobStatus.Snapshotting ||
                             j.Status == JobStatus.Diffing ||
                             j.Status == JobStatus.Translating ||
                             j.Status == JobStatus.Validating))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            LocalizationJob? next = candidates
                .Where(j =>
                    (j.Status is JobStatus.Queued or JobStatus.RetryWait or JobStatus.StaleConflict && j.AvailableAt <= now) ||
                    (j.Status is JobStatus.Snapshotting or JobStatus.Diffing or JobStatus.Translating or JobStatus.Validating &&
                     (!j.LeaseExpiresAt.HasValue || j.LeaseExpiresAt.Value < now)))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefault();

            if (next is null)
            {
                return null;
            }

            next.ClaimLease(workerId, now, LeaseDuration);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return next;
        }
        finally
        {
            FallbackLock.Release();
        }
    }
}
