using System.Text.RegularExpressions;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Validation;

public sealed partial class DeterministicTranslationValidator : ITranslationValidator
{
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeterministicTranslationValidator(
        IOptiFlowRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<ValidationBreakdown> ValidateAsync(
        string sourceText,
        TranslationResult modelResult,
        IReadOnlyList<GlossaryPair> glossary,
        ContentItemId contentItemId,
        ContentVersionId expectedSourceVersionId,
        CancellationToken cancellationToken)
    {
        var checks = new List<DeterministicCheckResult>(7);

        // 1. Structured JSON schema check
        bool schemaOk = modelResult.IsSuccess && modelResult.TranslatedText is not null;
        checks.Add(new DeterministicCheckResult(
            "JsonSchema",
            schemaOk,
            schemaOk ? null : modelResult.ErrorCode ?? "Invalid or missing structured JSON output."));

        string translated = modelResult.TranslatedText ?? string.Empty;

        // 2. Non-empty output
        bool nonEmptyOk = string.IsNullOrWhiteSpace(sourceText) || !string.IsNullOrWhiteSpace(translated);
        checks.Add(new DeterministicCheckResult(
            "NonEmpty",
            nonEmptyOk,
            nonEmptyOk ? null : "Translated text is empty for non-empty source."));

        // 3. HTML tag parity
        List<string> sourceTags = ExtractHtmlTags(sourceText);
        List<string> targetTags = ExtractHtmlTags(translated);
        bool htmlOk = sourceTags.SequenceEqual(targetTags, StringComparer.OrdinalIgnoreCase);
        checks.Add(new DeterministicCheckResult(
            "HtmlTagParity",
            htmlOk,
            htmlOk ? null : $"HTML tag mismatch (source={sourceTags.Count}, target={targetTags.Count})."));

        // 4. Placeholder parity
        List<string> sourcePlaceholders = ExtractPlaceholders(sourceText);
        List<string> targetPlaceholders = ExtractPlaceholders(translated);
        bool placeholdersOk = sourcePlaceholders.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(targetPlaceholders.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal);
        checks.Add(new DeterministicCheckResult(
            "PlaceholderParity",
            placeholdersOk,
            placeholdersOk ? null : "Placeholder tokens altered or missing."));

        // 5. Glossary compliance
        bool glossaryOk = true;
        string? glossaryDetail = null;
        foreach (GlossaryPair term in glossary)
        {
            if (sourceText.Contains(term.Term, StringComparison.OrdinalIgnoreCase))
            {
                if (!translated.Contains(term.PreferredTranslation, StringComparison.OrdinalIgnoreCase))
                {
                    glossaryOk = false;
                    glossaryDetail = "Preferred glossary term missing in translation.";
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(term.ForbiddenTranslation) &&
                translated.Contains(term.ForbiddenTranslation, StringComparison.OrdinalIgnoreCase))
            {
                glossaryOk = false;
                glossaryDetail = "Forbidden glossary term present in translation.";
                break;
            }
        }
        checks.Add(new DeterministicCheckResult("GlossaryCompliance", glossaryOk, glossaryDetail));

        // 6. Abnormal length growth guard
        bool lengthOk = true;
        string? lengthDetail = null;
        int sourceLen = sourceText.Trim().Length;
        int targetLen = translated.Trim().Length;
        if (sourceLen >= 4 && targetLen > sourceLen * 3.0)
        {
            lengthOk = false;
            lengthDetail = $"Abnormal length expansion ({targetLen} vs {sourceLen} chars).";
        }
        else if (sourceLen >= 20 && targetLen < sourceLen * 0.2)
        {
            lengthOk = false;
            lengthDetail = $"Suspicious truncation ({targetLen} vs {sourceLen} chars).";
        }
        checks.Add(new DeterministicCheckResult("LengthGuard", lengthOk, lengthDetail));

        // 7. Source version still current
        ContentItem? item = await _repository
            .GetContentItemAsync(_tenantContext.CurrentTenantId, contentItemId, cancellationToken)
            .ConfigureAwait(false);

        bool sourceCurrentOk = item is not null && item.LatestVersionId == expectedSourceVersionId;
        checks.Add(new DeterministicCheckResult(
            "SourceVersionCurrent",
            sourceCurrentOk,
            sourceCurrentOk ? null : "Source content version is stale."));

        // Note: modelResult.Confidence is intentionally NEVER used as an approval gate.
        bool allPassed = checks.All(c => c.Passed);
        string? summary = allPassed
            ? null
            : string.Join("; ", checks.Where(c => !c.Passed).Select(c => $"{c.CheckName}: {c.Detail}"));

        return new ValidationBreakdown(
            IsValid: allPassed,
            Checks: checks,
            SemanticCheckExecuted: false,
            SemanticScore: null,
            FailureSummary: summary);
    }

    private static List<string> ExtractHtmlTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return HtmlTagRegex()
            .Matches(text)
            .Select(m => m.Value.StartsWith("</", StringComparison.Ordinal)
                ? "/" + m.Groups[1].Value.ToLowerInvariant()
                : m.Groups[1].Value.ToLowerInvariant())
            .ToList();
    }

    private static List<string> ExtractPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return PlaceholderRegex()
            .Matches(text)
            .Select(m => m.Value)
            .ToList();
    }

    [GeneratedRegex(@"</?([a-zA-Z][a-zA-Z0-9:-]*)\b[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\{\{[^{}]+\}\}|\{[a-zA-Z0-9_.-]+\}|%[sdif]", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
