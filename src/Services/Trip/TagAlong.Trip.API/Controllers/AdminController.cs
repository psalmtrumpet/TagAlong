using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API.Controllers;

[ApiController]
[Route("api/admin/trips")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly TripDbContext _db;

    public AdminController(TripDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> ListTrips(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string type = "all",
        [FromQuery] string status = "all",
        CancellationToken cancellationToken = default)
    {
        var query = _db.Trips.IgnoreQueryFilters().Include(t => t.Stops).AsQueryable();

        if (type != "all")
            query = query.Where(t => t.TripType.ToString() == type);

        if (status != "all")
            query = query.Where(t => t.Status.ToString() == status);

        var total = await query.CountAsync(cancellationToken);

        var trips = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = trips.Select(t => new TripResponse(
            t.Id, t.TravelerId,
            t.Origin, t.OriginLatitude, t.OriginLongitude,
            t.Destination, t.DestinationLatitude, t.DestinationLongitude,
            t.DepartureTime, t.EstimatedArrivalTime, t.ActualArrivalTime,
            t.Status.ToString(), t.TripType.ToString(),
            t.AvailableCapacity, t.VehicleType, t.VehiclePlateNumber,
            t.Notes, t.MaxPackages, t.CurrentPackageCount,
            t.PassengerCapacity, t.CurrentPassengerCount,
            t.CurrentLatitude, t.CurrentLongitude, t.LocationUpdatedAt,
            t.Stops.Select(s => new TripStopResponse(
                s.Id, s.Location, s.Latitude, s.Longitude, s.Order, s.EstimatedTime, s.ActualArrivalTime)).ToList(),
            t.CreatedAt));

        return Ok(new { trips = dtos, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTrip(Guid id, CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .IgnoreQueryFilters()
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trip == null) return NotFound(new { error = "Trip not found" });

        return Ok(new TripResponse(
            trip.Id, trip.TravelerId,
            trip.Origin, trip.OriginLatitude, trip.OriginLongitude,
            trip.Destination, trip.DestinationLatitude, trip.DestinationLongitude,
            trip.DepartureTime, trip.EstimatedArrivalTime, trip.ActualArrivalTime,
            trip.Status.ToString(), trip.TripType.ToString(),
            trip.AvailableCapacity, trip.VehicleType, trip.VehiclePlateNumber,
            trip.Notes, trip.MaxPackages, trip.CurrentPackageCount,
            trip.PassengerCapacity, trip.CurrentPassengerCount,
            trip.CurrentLatitude, trip.CurrentLongitude, trip.LocationUpdatedAt,
            trip.Stops.Select(s => new TripStopResponse(
                s.Id, s.Location, s.Latitude, s.Longitude, s.Order, s.EstimatedTime, s.ActualArrivalTime)).ToList(),
            trip.CreatedAt));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var total      = await _db.Trips.IgnoreQueryFilters().CountAsync(cancellationToken);
        var scheduled  = await _db.Trips.IgnoreQueryFilters().CountAsync(t => t.Status.ToString() == "Scheduled", cancellationToken);
        var inProgress = await _db.Trips.IgnoreQueryFilters().CountAsync(t => t.Status.ToString() == "InProgress", cancellationToken);
        var completed  = await _db.Trips.IgnoreQueryFilters().CountAsync(t => t.Status.ToString() == "Completed", cancellationToken);
        var passenger  = await _db.Trips.IgnoreQueryFilters().CountAsync(t => t.TripType.ToString() == "Passenger", cancellationToken);
        var delivery   = await _db.Trips.IgnoreQueryFilters().CountAsync(t => t.TripType.ToString() == "Delivery", cancellationToken);

        return Ok(new { total, scheduled, inProgress, completed, passenger, delivery });
    }
}
