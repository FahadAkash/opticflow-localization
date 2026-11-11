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
    public SyncContentRequestValidator()
    {
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.ExternalContentKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Locale).MaximumLength(20).When(x => x.Locale is not null);
    }
}

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.ContentId).NotEmpty();
        RuleFor(x => x.SourceVersionId).NotEmpty();
        RuleFor(x => x.TargetLocales).NotEmpty();
        RuleForEach(x => x.TargetLocales).NotEmpty().MaximumLength(20);
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
