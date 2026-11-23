using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

/// <summary>
/// Paperwork for a job: proof of delivery, the signed loading note, the fridge display with the reading the driver
/// typed, the receiver's signature, or a problem report. <paramref name="ClientId"/> makes a resend safe;
/// <paramref name="CapturedAt"/> is when it was taken (possibly offline).
/// </summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record AddDocumentCommand(
    Guid JobId,
    JobDocumentKind Kind,
    Stream Content,
    string ContentType,
    long SizeBytes,
    double? Latitude,
    double? Longitude,
    double? TemperatureCelsius = null,
    string? Note = null,
    Guid? ClientId = null,
    DateTimeOffset? CapturedAt = null) : ICommand<JobDocumentDto>
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxPhotosPerKind = 10;
    public const int MaxSignatures = 3;
    public const int MaxTemperatureReadings = 50;
    public const int MaxNoteLength = 500;

    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
    };

}

public sealed class AddDocumentValidator : AbstractValidator<AddDocumentCommand>
{
    public AddDocumentValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ContentType).Must(AddDocumentCommand.AllowedTypes.ContainsKey).WithMessage("Photo must be JPEG, PNG, WebP or HEIC.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, AddDocumentCommand.MaxBytes).WithMessage("Photo must be smaller than 10 MB.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.TemperatureCelsius)
            .NotNull().WithMessage("Enter the temperature shown on the display.")
            .InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius)
            .WithMessage("That temperature looks wrong. Check the display and try again.")
            .When(x => x.Kind == JobDocumentKind.TemperaturePhoto);
        RuleFor(x => x.Note).NotEmpty().WithMessage("Say briefly what is wrong (e.g. 2 cartons crushed).")
            .When(x => x.Kind == JobDocumentKind.Exception);
        RuleFor(x => x.Note).NotEmpty().WithMessage("Enter the name of the person who signed.")
            .When(x => x.Kind == JobDocumentKind.Signature);
        RuleFor(x => x.Note).MaximumLength(AddDocumentCommand.MaxNoteLength);
    }
}

public sealed class AddDocumentHandler(
    IAppDbContext db, ITenantContext tenant, IFileStore files, IRealtimeNotifier notifier, TimeProvider clock, ILogger<AddDocumentHandler> logger)
    : ICommandHandler<AddDocumentCommand, JobDocumentDto>
{
    public async ValueTask<JobDocumentDto> Handle(AddDocumentCommand command, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, command.JobId, cancellationToken);

        // A resend of an upload the server already has: same answer, nothing stored twice.
        if (command.ClientId is { } clientId && job.Documents.FirstOrDefault(d => d.ClientId == clientId) is { } existing)
        {
            return JobDocumentDto.From(existing);
        }

        EnsureAllowed(job, command);

        var now = clock.GetUtcNow().UtcDateTime;
        var document = new JobDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = job.OrganizationId,
            JobId = job.Id,
            Kind = command.Kind,
            ContentType = command.ContentType,
            SizeBytes = command.SizeBytes,
            CapturedAtUtc = PhoneClock.Resolve(command.CapturedAt, job.CreatedAtUtc, now),
            ReceivedAtUtc = now,
            ClientId = command.ClientId,
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            TemperatureCelsius = command.Kind == JobDocumentKind.TemperaturePhoto ? command.TemperatureCelsius : null,
            Note = command.Kind is JobDocumentKind.Signature or JobDocumentKind.Exception && !string.IsNullOrWhiteSpace(command.Note)
                ? command.Note.Trim()
                : null,
        };
        document.StorageKey = $"{job.OrganizationId:N}/{job.Id:N}/{document.Id:N}.{AddDocumentCommand.AllowedTypes[command.ContentType]}";

        // File first: a failed database write leaves an orphan file, never a record pointing at nothing.
        await files.SaveAsync(document.StorageKey, command.Content, cancellationToken);
        db.JobDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        if (document.TemperatureCelsius is { } celsius && JobTemperaturePolicy.IsOutOfRange(job, celsius))
        {
            await AlertAsync(job, JobAlerts.TemperatureOutOfRange, $"Load outside its temperature range: {job.Vehicle.Name}",
                $"{job.CustomerName ?? job.Destination}: driver logged {celsius:0.#} °C, safe range {job.MinTempCelsius:0.#}–{job.MaxTempCelsius:0.#} °C.",
                cancellationToken);
        }

        if (document.Kind == JobDocumentKind.Exception)
        {
            await AlertAsync(job, JobAlerts.ProblemReported, $"Problem reported: {job.Vehicle.Name}",
                $"{job.CustomerName ?? job.Destination}: {document.Note}", cancellationToken);
        }

        return JobDocumentDto.From(document);
    }

    private static void EnsureAllowed(Job job, AddDocumentCommand command)
    {
        var count = job.Documents.Count(d => d.Kind == command.Kind);
        var open = job.Status is JobStatus.Planned or JobStatus.InProgress;
        switch (command.Kind)
        {
            case JobDocumentKind.PickupDocument:
            case JobDocumentKind.Exception:
                // Taken at loading or on the way; damage at loading matters as much as at delivery.
                if (!open)
                {
                    throw new BusinessRuleException("This job is closed; nothing more can be added.");
                }

                break;
            case JobDocumentKind.TemperaturePhoto:
                if (!open)
                {
                    throw new BusinessRuleException("This job is closed; temperature readings can no longer be added.");
                }

                if (count >= AddDocumentCommand.MaxTemperatureReadings)
                {
                    throw new BusinessRuleException($"A job can have at most {AddDocumentCommand.MaxTemperatureReadings} temperature readings.");
                }

                return;
            case JobDocumentKind.Signature:
                if (job.Status != JobStatus.InProgress)
                {
                    throw new BusinessRuleException(job.Status == JobStatus.Planned
                        ? "Start the job before taking the receiver's signature."
                        : "This job is closed; a signature can no longer be added.");
                }

                if (count >= AddDocumentCommand.MaxSignatures)
                {
                    throw new BusinessRuleException($"A job can have at most {AddDocumentCommand.MaxSignatures} signatures.");
                }

                return;
            default:
                if (job.Status != JobStatus.InProgress)
                {
                    throw new BusinessRuleException(job.Status == JobStatus.Planned
                        ? "Start the job before adding delivery photos."
                        : "This job is closed; photos can no longer be added.");
                }

                break;
        }

        if (count >= AddDocumentCommand.MaxPhotosPerKind)
        {
            throw new BusinessRuleException($"A job can have at most {AddDocumentCommand.MaxPhotosPerKind} photos of this kind.");
        }
    }

    private async Task AlertAsync(Job job, string kind, string title, string message, CancellationToken ct)
    {
        try
        {
            await notifier.AlertAsync(job.OrganizationId, kind, title, message, job.Id, ct);
        }
        catch (Exception ex)
        {
            // Never lose the paperwork because the live alert failed.
            logger.LogWarning(ex, "Alert {Kind} failed for job {JobId}", kind, job.Id);
        }
    }
}
