using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;
using OmniOps.Domain.Fleet;

namespace OmniOps.Application.Features.Fleet;

public sealed record VehicleDto(Guid Id, string Name, string? Registration, bool IsActive)
{
    public static VehicleDto From(Vehicle v) => new(v.Id, v.Name, v.Registration, v.IsActive);
}

[RequiresPolicy(Policies.Office)]
public sealed record ListVehiclesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<VehicleDto>>;

public sealed class ListVehiclesHandler(IAppDbContext db) : IQueryHandler<ListVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    public async ValueTask<IReadOnlyList<VehicleDto>> Handle(ListVehiclesQuery query, CancellationToken cancellationToken) =>
        await db.Vehicles.Where(v => query.IncludeInactive || v.IsActive).OrderBy(v => v.Name)
            .Select(v => new VehicleDto(v.Id, v.Name, v.Registration, v.IsActive)).ToListAsync(cancellationToken);
}

[RequiresPolicy(Policies.Office)]
public sealed record CreateVehicleCommand(string Name, string? Registration) : ICommand<VehicleDto>;

public sealed class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the truck a name, e.g. Truck-1 or its plate.").MaximumLength(50)
            .Matches("^[A-Za-z0-9 _-]+$").WithMessage("Use letters, numbers, spaces, - or _.");
        RuleFor(x => x.Registration).MaximumLength(30);
    }
}

public sealed class CreateVehicleHandler(IAppDbContext db, ITenantContext tenant, TimeProvider clock) : ICommandHandler<CreateVehicleCommand, VehicleDto>
{
    public async ValueTask<VehicleDto> Handle(CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.Vehicles.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new BusinessRuleException($"You already have a truck called {name}.");
        }

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            Name = name,
            Registration = string.IsNullOrWhiteSpace(command.Registration) ? null : command.Registration.Trim().ToUpperInvariant(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);
        return VehicleDto.From(vehicle);
    }
}
