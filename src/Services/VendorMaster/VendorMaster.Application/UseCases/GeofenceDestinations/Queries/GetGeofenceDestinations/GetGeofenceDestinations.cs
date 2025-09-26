namespace VendorMaster.Application.UseCases.GeofenceDestinations.Queries.GetGeofenceDestinations;

// request & result query get geofence destinations plant toyota
public record GetGeofenceDestinationsQuery(bool ActiveOnly = true) : IQuery<GetGeofenceDestinationsResult>;
public record GetGeofenceDestinationsResult(List<GeofenceDestinationDto> Destinations);

// handler use case get geofence plant toyota tmmin
public class GetGeofenceDestinationsHandler(IVendorMasterRepository repository)
    : IQueryHandler<GetGeofenceDestinationsQuery, GetGeofenceDestinationsResult>
{
    public async Task<GetGeofenceDestinationsResult> Handle(
        GetGeofenceDestinationsQuery query,
        CancellationToken cancellationToken)
    {
        var list = await repository.GetGeofenceDestinationsAsync(query.ActiveOnly, cancellationToken);

        var dtos = list.Select(d => new GeofenceDestinationDto
        {
            Id = d.Id,
            DestinationCode = d.DestinationCode,
            DestinationName = d.DestinationName,
            GeofenceType = d.GeofenceType,
            CenterLatitude = d.CenterLatitude,
            CenterLongitude = d.CenterLongitude,
            RadiusMeters = d.RadiusMeters,
            PolygonCoordinates = d.PolygonCoordinates,
            IsActive = d.IsActive
        }).ToList();

        return new GetGeofenceDestinationsResult(dtos);
    }
}
