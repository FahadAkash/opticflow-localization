using System.Text.RegularExpressions;
using FluentValidation;
using OptiFlow.Application.Contracts;

namespace OptiFlow.Application.Validators;

public sealed class CreateConnectionRequestValidator : AbstractValidator<CreateConnectionRequest>
{
    public CreateConnectionRequestValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
                         (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithMessage("BaseUrl must be a valid HTTP/HTTPS URL.");
        RuleFor(x => x.ApiSecret).NotEmpty().MaximumLength(1000);
    }
}

public sealed class SyncContentRequestValidator : AbstractValidator<SyncContentRequest>
{
    private static readonly Regex LocalePattern =
        new(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{2,4})?(?:-[a-zA-Z0-9]{2,3})?$", RegexOptions.Compiled);

    public SyncContentRequestValidator()
    {
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.ExternalContentKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Locale)
            .MaximumLength(15)
            .Matches(LocalePattern)
            .WithMessage("'{PropertyValue}' is not a valid BCP-47 / ISO 639 locale code (e.g. 'es-ES', 'fr-FR', 'BD', 'en').")
            .When(x => !string.IsNullOrWhiteSpace(x.Locale));
    }
}

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    private static readonly Regex LocalePattern =
        new(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{2,4})?(?:-[a-zA-Z0-9]{2,3})?$", RegexOptions.Compiled);

    public CreateJobRequestValidator()
    {
        RuleFor(x => x.ContentId).NotEmpty();
        RuleFor(x => x.SourceVersionId).NotEmpty();
        RuleFor(x => x.TargetLocales)
            .NotEmpty()
            .WithMessage("At least one target locale must be specified.");
        RuleForEach(x => x.TargetLocales)
            .NotEmpty()
            .MaximumLength(15)
            .Matches(LocalePattern)
            .WithMessage("'{PropertyValue}' is not a valid BCP-47 / ISO 639 locale code (e.g. 'es-ES', 'fr-FR', 'BD', 'en'). Gibberish and injection strings are prohibited.");
        RuleFor(x => x.IdempotencyKey).MaximumLength(200).When(x => x.IdempotencyKey is not null);
    }
}

public sealed class OptimizelyWebhookRequestValidator : AbstractValidator<OptimizelyWebhookRequest>
{
    public OptimizelyWebhookRequestValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExternalEventId).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.ExternalContentKey).NotEmpty().MaximumLength(500);
    }
}
