namespace VendorMaster.Application.UseCases.GeofenceDestinations.Queries.GetGeofenceDestinations;

// request & result query get geofence destinations plant toyota
public record GetGeofenceDestinationsQuery(bool ActiveOnly = true) : IQuery<GetGeofenceDestinationsResult>;
public record GetGeofenceDestinationsResult(List<GeofenceDestinationDto> Destinations);

// handler use case get geofence plant toyota tmmin
public class GetGeofenceDestinationsHandler(
    IVendorMasterRepository repository,
    IMapper mapper)
    : IQueryHandler<GetGeofenceDestinationsQuery, GetGeofenceDestinationsResult>
{
    public async Task<GetGeofenceDestinationsResult> Handle(
        GetGeofenceDestinationsQuery query,
        CancellationToken cancellationToken)
    {
        var list = await repository.GetGeofenceDestinationsAsync(query.ActiveOnly, cancellationToken);
        var dtos = mapper.Map<List<GeofenceDestinationDto>>(list);

        return new GetGeofenceDestinationsResult(dtos);
    }
}
