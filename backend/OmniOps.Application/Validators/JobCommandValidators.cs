using FluentValidation;
using OmniOps.Application.Commands;

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
                .Must(t => t > DateTime.UtcNow.AddDays(-7) && t < DateTime.UtcNow.AddMinutes(5))
                .WithMessage("Point time must be within the last 7 days and not in the future.");
        });
    }
}

public class AddJobPhotoCommandValidator : AbstractValidator<AddJobPhotoCommand>
{
    public AddJobPhotoCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.ContentType).Must(t => AddJobPhotoCommand.AllowedTypes.ContainsKey(t))
            .WithMessage("Photo must be JPEG, PNG, WebP or HEIC.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, AddJobPhotoCommand.MaxBytes)
            .WithMessage("Photo must be smaller than 10 MB.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
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
