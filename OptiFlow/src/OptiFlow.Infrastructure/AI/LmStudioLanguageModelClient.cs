using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Infrastructure.AI;

public sealed class LmStudioOptions
{
    public const string SectionName = "LMStudio";
    public string BaseUrl { get; set; } = "http://localhost:1234";
    public string ModelName { get; set; } = "google/gemma-4-12b-qat";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxConcurrency { get; set; } = 1;
}

public sealed class LmStudioLanguageModelClient : ILanguageModelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LmStudioOptions _options;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<LmStudioLanguageModelClient> _logger;

    public LmStudioLanguageModelClient(
        HttpClient httpClient,
        IOptions<LmStudioOptions> options,
        ILogger<LmStudioLanguageModelClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _concurrencyGate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency), Math.Max(1, _options.MaxConcurrency));
        _logger = logger;
    }

    public async Task<bool> WarmUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync("/v1/models", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("LM Studio warm-up probe unavailable ({Reason})", ex.GetType().Name);
            return false;
        }
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();

        try
        {
            string inputPayload = JsonSerializer.Serialize(new
            {
                source_locale = request.SourceLocale,
                target_locale = request.TargetLocale,
                brand = request.Brand,
                content_type = request.ContentType,
                source_text = request.SourceText,
                glossary = request.Glossary.Select(g => new
                {
                    term = g.Term,
                    preferred_translation = g.PreferredTranslation
                })
            }, JsonOptions);

            var chatPayload = new
            {
                model = _options.ModelName,
                temperature = 0.1,
                max_tokens = 2048,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a localization engine. Return ONLY strict JSON: {\"translated_text\": string, \"confidence\": number}. Preserve all HTML tags, placeholders, and glossary terms exactly."
                    },
                    new
                    {
                        role = "user",
                        content = inputPayload
                    }
                }
            };

            using var httpContent = new StringContent(
                JsonSerializer.Serialize(chatPayload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response = await _httpClient
                .PostAsync("/v1/chat/completions", httpContent, cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();
            int latencyMs = (int)sw.ElapsedMilliseconds;

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new TranslationResult(
                    false, null, null, null, 0, 0, latencyMs,
                    _options.ModelName, "LMStudio", AiInvocationStatus.PermanentError,
                    IsTransientError: false, ErrorCode: ErrorCodes.PermissionDenied);
            }

            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                return new TranslationResult(
                    false, null, null, null, 0, 0, latencyMs,
                    _options.ModelName, "LMStudio", AiInvocationStatus.TransientError,
                    IsTransientError: true, ErrorCode: ErrorCodes.TransientFailure);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new TranslationResult(
                    false, null, null, null, 0, 0, latencyMs,
                    _options.ModelName, "LMStudio", AiInvocationStatus.PermanentError,
                    IsTransientError: false, ErrorCode: ErrorCodes.ValidationFailed);
            }

            string rawBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ChatCompletionResponse? completion = JsonSerializer.Deserialize<ChatCompletionResponse>(rawBody, JsonOptions);

            int inputTokens = completion?.Usage?.PromptTokens ?? 0;
            int outputTokens = completion?.Usage?.CompletionTokens ?? 0;
            string? messageContent = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(messageContent))
            {
                messageContent = completion?.Choices?.FirstOrDefault()?.Message?.ReasoningContent;
            }

            if (string.IsNullOrWhiteSpace(messageContent))
            {
                return new TranslationResult(
                    false, rawBody, null, null, inputTokens, outputTokens, latencyMs,
                    _options.ModelName, "LMStudio", AiInvocationStatus.ValidationFailed,
                    IsTransientError: false, ErrorCode: "EMPTY_MODEL_RESPONSE");
            }

            string cleanedJson = StripCodeFences(messageContent);
            ModelTranslationPayload? parsed = JsonSerializer.Deserialize<ModelTranslationPayload>(cleanedJson, JsonOptions);

            if (parsed is null || parsed.TranslatedText is null)
            {
                return new TranslationResult(
                    false, messageContent, null, null, inputTokens, outputTokens, latencyMs,
                    _options.ModelName, "LMStudio", AiInvocationStatus.ValidationFailed,
                    IsTransientError: false, ErrorCode: "INVALID_JSON_SCHEMA");
            }

            return new TranslationResult(
                true,
                cleanedJson,
                parsed.TranslatedText,
                parsed.Confidence,
                inputTokens,
                outputTokens,
                latencyMs,
                _options.ModelName,
                "LMStudio",
                AiInvocationStatus.Succeeded,
                IsTransientError: false);
        }
        catch (JsonException)
        {
            sw.Stop();
            return new TranslationResult(
                false, null, null, null, 0, 0, (int)sw.ElapsedMilliseconds,
                _options.ModelName, "LMStudio", AiInvocationStatus.ValidationFailed,
                IsTransientError: false, ErrorCode: "INVALID_JSON_SYNTAX");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new TranslationResult(
                false, null, null, null, 0, 0, (int)sw.ElapsedMilliseconds,
                _options.ModelName, "LMStudio", AiInvocationStatus.TimedOut,
                IsTransientError: true, ErrorCode: "AI_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            sw.Stop();
            return new TranslationResult(
                false, null, null, null, 0, 0, (int)sw.ElapsedMilliseconds,
                _options.ModelName, "LMStudio", AiInvocationStatus.TransientError,
                IsTransientError: true, ErrorCode: ErrorCodes.TransientFailure);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private static string StripCodeFences(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                return trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }
        return trimmed;
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; set; }
        public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }

        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }

    private sealed class ModelTranslationPayload
    {
        [JsonPropertyName("translated_text")]
        public string? TranslatedText { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }
}
