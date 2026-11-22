using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Events;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class CreateFleetCommandHandler : IRequestHandler<CreateFleetCommand, FleetDto>
{
    private readonly IFleetRepository _fleetRepository;
    private readonly ITenantContext _tenant;

    public CreateFleetCommandHandler(IFleetRepository fleetRepository, ITenantContext tenant)
    {
        _fleetRepository = fleetRepository;
        _tenant = tenant;
    }

    public async Task<FleetDto> Handle(CreateFleetCommand request, CancellationToken cancellationToken)
    {
        var fleet = new Fleet
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            WorkspaceId = _tenant.WorkspaceId ?? Guid.Empty,
            Name = request.Name,
            Description = request.Description,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _fleetRepository.AddAsync(fleet, cancellationToken);
        await _fleetRepository.SaveChangesAsync(cancellationToken);
        return FleetDto.FromEntity(fleet);
    }
}

public class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, VehicleDto>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITenantScope _scope;

    public CreateVehicleCommandHandler(IVehicleRepository vehicleRepository, ITenantScope scope)
    {
        _vehicleRepository = vehicleRepository;
        _scope = scope;
    }

    public async Task<VehicleDto> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            FleetId = request.FleetId,
            ExternalId = request.ExternalId,
            Vin = request.Vin,
            Registration = request.Registration,
            Status = VehicleOperationalStatus.Active
        };
        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        await _vehicleRepository.SaveChangesAsync(cancellationToken);
        return VehicleDto.FromEntity(vehicle);
    }
}

public class CreateDriverCommandHandler : IRequestHandler<CreateDriverCommand, DriverDto>
{
    private readonly IDriverRepository _driverRepository;
    private readonly ITenantScope _scope;

    public CreateDriverCommandHandler(IDriverRepository driverRepository, ITenantScope scope)
    {
        _driverRepository = driverRepository;
        _scope = scope;
    }

    public async Task<DriverDto> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            FleetId = request.FleetId,
            FullName = request.FullName.Trim(),
            LicenseNumber = request.LicenseNumber,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
        };
        await _driverRepository.AddAsync(driver, cancellationToken);
        await _driverRepository.SaveChangesAsync(cancellationToken);
        return DriverDto.FromEntity(driver);
    }
}

public class UpdateDriverCommandHandler : IRequestHandler<UpdateDriverCommand, DriverDto>
{
    private readonly IDriverRepository _drivers;
    private readonly ITenantScope _scope;

    public UpdateDriverCommandHandler(IDriverRepository drivers, ITenantScope scope)
    {
        _drivers = drivers;
        _scope = scope;
    }

    public async Task<DriverDto> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureDriverAsync(request.DriverId, cancellationToken);
        var driver = await _drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Driver {request.DriverId} not found.");
        driver.FullName = request.FullName.Trim();
        driver.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        await _drivers.SaveChangesAsync(cancellationToken);
        return DriverDto.FromEntity(driver);
    }
}

public class AssignDriverToVehicleCommandHandler : IRequestHandler<AssignDriverToVehicleCommand, VehicleDto>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;

    private readonly ITenantScope _scope;

    public AssignDriverToVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        ITenantScope scope)
    {
        _scope = scope;
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
    }

    public async Task<VehicleDto> Handle(AssignDriverToVehicleCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureVehicleAsync(request.VehicleId, cancellationToken);
        await _scope.EnsureDriverAsync(request.DriverId, cancellationToken);
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Driver {request.DriverId} not found.");

        vehicle.AssignedDriverId = driver.Id;
        await _vehicleRepository.SaveChangesAsync(cancellationToken);
        return VehicleDto.FromEntity(vehicle);
    }
}

public class CreateDepotCommandHandler : IRequestHandler<CreateDepotCommand, DepotDto>
{
    private readonly IDepotRepository _depotRepository;
    private readonly ITenantScope _scope;

    public CreateDepotCommandHandler(IDepotRepository depotRepository, ITenantScope scope)
    {
        _depotRepository = depotRepository;
        _scope = scope;
    }

    public async Task<DepotDto> Handle(CreateDepotCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var depot = new Depot
        {
            Id = Guid.NewGuid(),
            FleetId = request.FleetId,
            Name = request.Name,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Capacity = request.Capacity
        };
        await _depotRepository.AddAsync(depot, cancellationToken);
        await _depotRepository.SaveChangesAsync(cancellationToken);
        return DepotDto.FromEntity(depot);
    }
}

public class CreateGeofenceCommandHandler : IRequestHandler<CreateGeofenceCommand, GeofenceDto>
{
    private readonly IGeofenceRepository _geofenceRepository;

    private readonly ITenantScope _scope;

    public CreateGeofenceCommandHandler(IGeofenceRepository geofenceRepository, ITenantScope scope)
    {
        _geofenceRepository = geofenceRepository;
        _scope = scope;
    }

    public async Task<GeofenceDto> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
    {
        // A zone must belong to one of the caller's fleets; zones without a fleet would be visible to nobody.
        if (request.FleetId is not { } fleetId)
        {
            throw new ArgumentException("A zone needs a fleet.");
        }

        await _scope.EnsureFleetAsync(fleetId, cancellationToken);
        if (!Enum.TryParse<GeofenceShapeType>(request.ShapeType, true, out var shapeType))
        {
            throw new ArgumentException($"Invalid geofence shape type: {request.ShapeType}");
        }

        var geofence = new Geofence
        {
            Id = Guid.NewGuid(),
            FleetId = request.FleetId,
            Name = request.Name,
            ShapeType = shapeType,
            CenterLatitude = request.CenterLatitude,
            CenterLongitude = request.CenterLongitude,
            RadiusMeters = request.RadiusMeters,
            PolygonCoordinatesJson = request.PolygonCoordinatesJson,
            IsActive = true
        };
        await _geofenceRepository.AddAsync(geofence, cancellationToken);
        await _geofenceRepository.SaveChangesAsync(cancellationToken);
        return GeofenceDto.FromEntity(geofence);
    }
}

public class ReplayEventsCommandHandler : IRequestHandler<ReplayEventsCommand, int>
{
    private readonly IEventReplayService _replayService;

    public ReplayEventsCommandHandler(IEventReplayService replayService) => _replayService = replayService;

    public Task<int> Handle(ReplayEventsCommand request, CancellationToken cancellationToken) =>
        _replayService.ReplayAsync(request.FromUtc, request.ToUtc, request.EventType, cancellationToken);
}
