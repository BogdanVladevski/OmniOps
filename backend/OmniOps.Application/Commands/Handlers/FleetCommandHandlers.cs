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

    public CreateFleetCommandHandler(IFleetRepository fleetRepository) => _fleetRepository = fleetRepository;

    public async Task<FleetDto> Handle(CreateFleetCommand request, CancellationToken cancellationToken)
    {
        var fleet = new Fleet
        {
            Id = Guid.NewGuid(),
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

    public CreateVehicleCommandHandler(IVehicleRepository vehicleRepository) => _vehicleRepository = vehicleRepository;

    public async Task<VehicleDto> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
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

    public CreateDriverCommandHandler(IDriverRepository driverRepository) => _driverRepository = driverRepository;

    public async Task<DriverDto> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            FleetId = request.FleetId,
            FullName = request.FullName,
            LicenseNumber = request.LicenseNumber
        };
        await _driverRepository.AddAsync(driver, cancellationToken);
        await _driverRepository.SaveChangesAsync(cancellationToken);
        return DriverDto.FromEntity(driver);
    }
}

public class AssignDriverToVehicleCommandHandler : IRequestHandler<AssignDriverToVehicleCommand, VehicleDto>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;

    public AssignDriverToVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository)
    {
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
    }

    public async Task<VehicleDto> Handle(AssignDriverToVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Driver {request.DriverId} not found.");

        vehicle.AssignedDriverId = driver.Id;
        await _vehicleRepository.SaveChangesAsync(cancellationToken);
        return VehicleDto.FromEntity(vehicle);
    }
}

public class CreateTripCommandHandler : IRequestHandler<CreateTripCommand, TripDto>
{
    private readonly ITripRepository _tripRepository;

    public CreateTripCommandHandler(ITripRepository tripRepository) => _tripRepository = tripRepository;

    public async Task<TripDto> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            Origin = request.Origin,
            Destination = request.Destination,
            Status = TripStatus.Planned
        };
        await _tripRepository.AddAsync(trip, cancellationToken);
        await _tripRepository.SaveChangesAsync(cancellationToken);
        return TripDto.FromEntity(trip);
    }
}

public class StartTripCommandHandler : IRequestHandler<StartTripCommand, TripDto>
{
    private readonly ITripRepository _tripRepository;

    public StartTripCommandHandler(ITripRepository tripRepository) => _tripRepository = tripRepository;

    public async Task<TripDto> Handle(StartTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new KeyNotFoundException($"Trip {request.TripId} not found.");
        trip.Status = TripStatus.InProgress;
        trip.StartedAtUtc = DateTime.UtcNow;
        await _tripRepository.SaveChangesAsync(cancellationToken);
        return TripDto.FromEntity(trip);
    }
}

public class CompleteTripCommandHandler : IRequestHandler<CompleteTripCommand, TripDto>
{
    private readonly ITripRepository _tripRepository;

    public CompleteTripCommandHandler(ITripRepository tripRepository) => _tripRepository = tripRepository;

    public async Task<TripDto> Handle(CompleteTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new KeyNotFoundException($"Trip {request.TripId} not found.");
        trip.Status = TripStatus.Completed;
        trip.CompletedAtUtc = DateTime.UtcNow;
        await _tripRepository.SaveChangesAsync(cancellationToken);
        return TripDto.FromEntity(trip);
    }
}

public class CreateDepotCommandHandler : IRequestHandler<CreateDepotCommand, DepotDto>
{
    private readonly IDepotRepository _depotRepository;

    public CreateDepotCommandHandler(IDepotRepository depotRepository) => _depotRepository = depotRepository;

    public async Task<DepotDto> Handle(CreateDepotCommand request, CancellationToken cancellationToken)
    {
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

    public CreateGeofenceCommandHandler(IGeofenceRepository geofenceRepository) =>
        _geofenceRepository = geofenceRepository;

    public async Task<GeofenceDto> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
    {
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
