using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Fleet;

namespace OmniOps.Application.Features.Fleet;

public sealed record DriverDto(Guid Id, string FullName, string? Phone, bool IsActive)
{
    public static DriverDto From(Driver d) => new(d.Id, d.FullName, d.Phone, d.IsActive);
}

[RequiresPolicy(Policies.Office)]
public sealed record ListDriversQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<DriverDto>>;

public sealed class ListDriversHandler(IAppDbContext db) : IQueryHandler<ListDriversQuery, IReadOnlyList<DriverDto>>
{
    public async ValueTask<IReadOnlyList<DriverDto>> Handle(ListDriversQuery query, CancellationToken cancellationToken) =>
        await db.Drivers.Where(d => query.IncludeInactive || d.IsActive).OrderBy(d => d.FullName)
            .Select(d => new DriverDto(d.Id, d.FullName, d.Phone, d.IsActive)).ToListAsync(cancellationToken);
}

[RequiresPolicy(Policies.Office)]
public sealed record CreateDriverCommand(string FullName, string? Phone) : ICommand<DriverDto>;

public sealed class CreateDriverValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Enter the driver's name.").MaximumLength(200);
        RuleFor(x => x.Phone).PhoneNumber();
    }
}

public sealed class CreateDriverHandler(IAppDbContext db, ITenantContext tenant, TimeProvider clock) : ICommandHandler<CreateDriverCommand, DriverDto>
{
    public async ValueTask<DriverDto> Handle(CreateDriverCommand command, CancellationToken cancellationToken)
    {
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            FullName = command.FullName.Trim(),
            Phone = FleetText.Clean(command.Phone),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync(cancellationToken);
        return DriverDto.From(driver);
    }
}

[RequiresPolicy(Policies.Office)]
public sealed record UpdateDriverCommand(Guid DriverId, string FullName, string? Phone) : ICommand<DriverDto>;

public sealed class UpdateDriverValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Enter the driver's name.").MaximumLength(200);
        RuleFor(x => x.Phone).PhoneNumber();
    }
}

public sealed class UpdateDriverHandler(IAppDbContext db) : ICommandHandler<UpdateDriverCommand, DriverDto>
{
    public async ValueTask<DriverDto> Handle(UpdateDriverCommand command, CancellationToken cancellationToken)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == command.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException("Driver not found.");
        driver.FullName = command.FullName.Trim();
        driver.Phone = FleetText.Clean(command.Phone);
        await db.SaveChangesAsync(cancellationToken);
        return DriverDto.From(driver);
    }
}

internal static partial class FleetText
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static IRuleBuilderOptions<T, string?> PhoneNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(p => string.IsNullOrWhiteSpace(p) || PhonePattern().IsMatch(p.Trim()))
            .WithMessage("Enter a phone number like +389 70 123 456.");

    [System.Text.RegularExpressions.GeneratedRegex(@"^\+?[0-9 ()/-]{5,30}$")]
    private static partial System.Text.RegularExpressions.Regex PhonePattern();
}
