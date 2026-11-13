using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OptiFlow.Domain.ValueObjects;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => Value.ToString();
}

public readonly record struct ConnectionId(Guid Value)
{
    public static ConnectionId New() => new(Guid.NewGuid());
}

public readonly record struct ContentItemId(Guid Value)
{
    public static ContentItemId New() => new(Guid.NewGuid());
}

public readonly record struct ContentVersionId(Guid Value)
{
    public static ContentVersionId New() => new(Guid.NewGuid());
}

public readonly record struct ContentBlockId(Guid Value)
{
    public static ContentBlockId New() => new(Guid.NewGuid());
}

public readonly record struct TranslationId(Guid Value)
{
    public static TranslationId New() => new(Guid.NewGuid());
}

public readonly record struct JobId(Guid Value)
{
    public static JobId New() => new(Guid.NewGuid());
}

public readonly record struct JobBlockId(Guid Value)
{
    public static JobBlockId New() => new(Guid.NewGuid());
}

public readonly record struct TranslationMemoryId(Guid Value)
{
    public static TranslationMemoryId New() => new(Guid.NewGuid());
}

public readonly record struct GlossaryTermId(Guid Value)
{
    public static GlossaryTermId New() => new(Guid.NewGuid());
}

public readonly record struct ReviewTaskId(Guid Value)
{
    public static ReviewTaskId New() => new(Guid.NewGuid());
}

public readonly record struct AiInvocationId(Guid Value)
{
    public static AiInvocationId New() => new(Guid.NewGuid());
}

public readonly record struct AuditEventId(Guid Value)
{
    public static AuditEventId New() => new(Guid.NewGuid());
}

public readonly record struct WebhookInboxId(Guid Value)
{
    public static WebhookInboxId New() => new(Guid.NewGuid());
}

public readonly record struct OutboxMessageId(Guid Value)
{
    public static OutboxMessageId New() => new(Guid.NewGuid());
}

public sealed record ContentKey
{
    public string Value { get; }

    public ContentKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record BlockKey
{
    public string Value { get; }

    public BlockKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record LocaleCode
{
    private static readonly Regex LocalePattern =
        new(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{2,4})?(?:-[a-zA-Z0-9]{2,3})?$", RegexOptions.Compiled);

    public string Value { get; }

    public LocaleCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string trimmed = value.Trim();
        return trimmed.Length <= 15 && LocalePattern.IsMatch(trimmed);
    }

    public override string ToString() => Value;
}

public sealed record IdempotencyKey
{
    public string Value { get; }

    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record ContentHash
{
    public string Value { get; }

    public ContentHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64)
        {
            throw new ArgumentException("SHA-256 hash must be 64 hex characters.", nameof(value));
        }
        Value = normalized;
    }

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string unifiedNewlines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                                     .Replace('\r', '\n');
        var sb = new StringBuilder(unifiedNewlines.Length);
        bool previousWasWhitespace = false;

        foreach (char ch in unifiedNewlines.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    sb.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                sb.Append(ch);
                previousWasWhitespace = false;
            }
        }

        return sb.ToString();
    }

    public static ContentHash FromRawText(string rawText)
    {
        string normalized = NormalizeText(rawText);
        byte[] bytes = Encoding.UTF8.GetBytes(normalized);
        byte[] digest = SHA256.HashData(bytes);
        return new ContentHash(Convert.ToHexString(digest).ToLowerInvariant());
    }

    public override string ToString() => Value;
}
