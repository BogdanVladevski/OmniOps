using FluentValidation;
using OmniOps.Application.Commands;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Validators;

public class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.DriverId).NotEqual(Guid.Empty).When(x => x.DriverId.HasValue);
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Origin).MaximumLength(300);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DestinationLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestinationLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x)
            .Must(x => x.DestinationLatitude.HasValue == x.DestinationLongitude.HasValue)
            .WithMessage("Give both destination coordinates or neither.");
        RuleFor(x => x)
            .Must(x => x.MinTempCelsius.HasValue == x.MaxTempCelsius.HasValue)
            .WithMessage("Give both ends of the temperature range or neither.");
        RuleFor(x => x)
            .Must(x => x.MinTempCelsius is null || x.MinTempCelsius < x.MaxTempCelsius)
            .WithMessage("The lowest temperature must be below the highest.");
        RuleFor(x => x.MinTempCelsius).InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius);
        RuleFor(x => x.MaxTempCelsius).InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius);
    }
}

public class RecordJobLocationsCommandValidator : AbstractValidator<RecordJobLocationsCommand>
{
    public const int MaxPointsPerBatch = 500;

    public RecordJobLocationsCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Points).NotNull().Must(p => p.Count is > 0 and <= MaxPointsPerBatch)
            .WithMessage($"Send between 1 and {MaxPointsPerBatch} points per request.");
        RuleForEach(x => x.Points).ChildRules(point =>
        {
            point.RuleFor(p => p.Id).NotEmpty();
            point.RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
            point.RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
            point.RuleFor(p => p.SpeedKmh).InclusiveBetween(0, 300).When(p => p.SpeedKmh.HasValue);
            point.RuleFor(p => p.Heading).InclusiveBetween(0, 360).When(p => p.Heading.HasValue);
            point.RuleFor(p => p.AccuracyMeters).GreaterThanOrEqualTo(0).When(p => p.AccuracyMeters.HasValue);
            point.RuleFor(p => p.RecordedAtUtc)
                .Must(t => t > DateTimeOffset.UtcNow.AddDays(-7) && t < DateTimeOffset.UtcNow.AddMinutes(5))
                .WithMessage("Point time must be within the last 7 days and not in the future.");
        });
    }
}

public class AddJobDocumentCommandValidator : AbstractValidator<AddJobDocumentCommand>
{
    public AddJobDocumentCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.ContentType).Must(t => AddJobDocumentCommand.AllowedTypes.ContainsKey(t))
            .WithMessage("Photo must be JPEG, PNG, WebP or HEIC.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, AddJobDocumentCommand.MaxBytes)
            .WithMessage("Photo must be smaller than 10 MB.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.TemperatureCelsius)
            .NotNull().WithMessage("Enter the temperature shown on the display.")
            .InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius)
            .WithMessage("That temperature looks wrong. Check the display and try again.")
            .When(x => x.Kind == TripDocumentKind.TemperaturePhoto);
        RuleFor(x => x.Note).NotEmpty().WithMessage("Say briefly what is wrong (e.g. 2 cartons crushed).")
            .When(x => x.Kind == TripDocumentKind.Exception);
        RuleFor(x => x.Note).NotEmpty().WithMessage("Enter the name of the person who signed.")
            .When(x => x.Kind == TripDocumentKind.Signature);
        RuleFor(x => x.Note).MaximumLength(AddJobDocumentCommand.MaxNoteLength);
    }
}

public class DeliverJobCommandValidator : AbstractValidator<DeliverJobCommand>
{
    public DeliverJobCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.RecipientName).NotEmpty().WithMessage("Enter the name of the person who received the goods.")
            .MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}
