using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Entities;

public sealed class Tenant
{
    public TenantId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tenant() { }

    public Tenant(TenantId id, string name, string slug, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
    }
}

public sealed class CmsConnection
{
    public ConnectionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public CmsProvider Provider { get; private set; }
    public string BaseUrl { get; private set; } = string.Empty;
    /// <summary>Vault or protected secret reference, never a plaintext credential.</summary>
    public string CredentialRef { get; private set; } = string.Empty;
    public CmsConnectionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CmsConnection() { }

    public CmsConnection(
        ConnectionId id,
        TenantId tenantId,
        CmsProvider provider,
        string baseUrl,
        string credentialRef,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialRef);

        Id = id;
        TenantId = tenantId;
        Provider = provider;
        BaseUrl = baseUrl.Trim();
        CredentialRef = credentialRef.Trim();
        Status = CmsConnectionStatus.Active;
        CreatedAt = createdAt;
    }
}
