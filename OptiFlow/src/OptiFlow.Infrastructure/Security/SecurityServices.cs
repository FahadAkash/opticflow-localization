using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Security;

public sealed class TenantContext : ITenantContext
{
    public TenantId CurrentTenantId { get; private set; } = TenantId.Empty;
    public string CurrentActorId { get; private set; } = "anonymous";
    public bool HasTenant => !CurrentTenantId.IsEmpty;

    public void SetTenant(TenantId tenantId, string actorId)
    {
        if (tenantId.IsEmpty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        CurrentTenantId = tenantId;
        CurrentActorId = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId.Trim();
    }
}

public sealed class DataProtectionCredentialProtector : ICredentialProtector
{
    private const string VaultPrefix = "dp-v1:";
    private readonly IDataProtector _protector;

    public DataProtectionCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("OptiFlow.CmsCredentials.v1");
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return VaultPrefix + _protector.Protect(plaintext);
    }

    public string Unprotect(string credentialRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialRef);
        if (!credentialRef.StartsWith(VaultPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unrecognized credential reference format.");
        }
        return _protector.Unprotect(credentialRef[VaultPrefix.Length..]);
    }
}

public sealed class HmacWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly ICredentialProtector _credentialProtector;

    public HmacWebhookSignatureVerifier(ICredentialProtector credentialProtector)
    {
        _credentialProtector = credentialProtector;
    }

    public bool VerifySignature(string payload, string? signatureHeader, string secretRef)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        string secret = _credentialProtector.Unprotect(secretRef);
        string expectedHex = ComputeHmacSha256Hex(payload, secret);
        string normalizedHeader = signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signatureHeader["sha256=".Length..]
            : signatureHeader;

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant());
        byte[] actualBytes = Encoding.UTF8.GetBytes(normalizedHeader.Trim().ToLowerInvariant());

        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static string ComputeHmacSha256Hex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
