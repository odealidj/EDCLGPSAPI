namespace GPSInterfacing.Mapping;

public class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<GeofenceMasterAuthDto, GpsVendorAuth>()
            .Map(dest => dest.Headers, src => src.Headers.ToJsonObject());
    }
}