using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Cache;

public sealed class RedisCacheService : ICacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        ILogger<RedisCacheService> logger,
        IDistributedCache? distributedCache = null)
    {
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task<T?> GetAsync<T>(TenantId tenantId, string key, CancellationToken cancellationToken)
        where T : class
    {
        if (_distributedCache is null)
        {
            return null;
        }

        try
        {
            string scopedKey = BuildTenantKey(tenantId, key);
            string? json = await _distributedCache.GetStringAsync(scopedKey, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Optional Redis cache: degrade gracefully to PostgreSQL
            _logger.LogWarning(ex, "Redis GET failed for tenant {TenantId}; falling back to PostgreSQL", tenantId.Value);
            return null;
        }
    }

    public async Task SetAsync<T>(TenantId tenantId, string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
        where T : class
    {
        if (_distributedCache is null)
        {
            return;
        }

        try
        {
            string scopedKey = BuildTenantKey(tenantId, key);
            string json = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(
                scopedKey,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis SET failed for tenant {TenantId}; continuing without cache", tenantId.Value);
        }
    }

    public async Task RemoveAsync(TenantId tenantId, string key, CancellationToken cancellationToken)
    {
        if (_distributedCache is null)
        {
            return;
        }

        try
        {
            await _distributedCache.RemoveAsync(BuildTenantKey(tenantId, key), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis REMOVE failed for tenant {TenantId}", tenantId.Value);
        }
    }

    public static string BuildTenantKey(TenantId tenantId, string key) => $"tenant:{tenantId.Value}:{key}";
}
