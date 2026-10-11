using System.Text.Json.Nodes;
using AutoMapper;
using VendorMaster.Application.DTOs;
using VendorMaster.Domain.Entities;

namespace VendorMaster.Application.Mappings;

/// <summary>
/// AutoMapper profile untuk transformasi entitas domain VendorMaster ke DTO dan sebaliknya.
/// Mengikuti best practice Clean Architecture & CQRS.
/// </summary>
public class VendorMappingProfile : Profile
{
    public VendorMappingProfile()
    {
        CreateMap<JsonObject, JsonObject>()
            .ConvertUsing(src => (JsonObject)CloneJson(src)!);

        ConfigureEndpointMapping();
        ConfigureAuthMapping();
        ConfigureSimpleChildMappings();
        ConfigureVendorMapping();
        ConfigureGeofenceMapping();
    }

    private static JsonNode? CloneJson(JsonNode? src) => src?.DeepClone();

    private void ConfigureEndpointMapping()
    {
        CreateMap<GpsVendorEndpoint, VendorEndpointDto>()
            .ConstructUsing(_ => new VendorEndpointDto())
            .ForMember(dest => dest.GpsVendorId, opt => opt.MapFrom(src => src.VendorId))
            .ForMember(dest => dest.Headers, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Headers)))
            .ForMember(dest => dest.Params, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Params)))
            .ForMember(dest => dest.Bodies, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Bodies)))
            .ForMember(dest => dest.VarParams, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.VarParams)));
    }

    private void ConfigureAuthMapping()
    {
        CreateMap<GpsVendorAuth, VendorAuthDto>()
            .ConstructUsing(_ => new VendorAuthDto())
            .ForMember(dest => dest.GpsVendorId, opt => opt.MapFrom(src => src.VendorId))
            .ForMember(dest => dest.Authtype, opt => opt.MapFrom(src => src.AuthType))
            .ForMember(dest => dest.Headers, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Headers)))
            .ForMember(dest => dest.Params, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Params)))
            .ForMember(dest => dest.Bodies, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.Bodies)));
    }

    private void ConfigureSimpleChildMappings()
    {
        CreateMap<GpsVendorMapping, VendorMappingDto>()
            .ConstructUsing(_ => new VendorMappingDto())
            .ForMember(dest => dest.GpsVendorId, opt => opt.MapFrom(src => src.VendorId));

        CreateMap<GpsVendorLpcd, VendorLpcdDto>()
            .ConstructUsing(_ => new VendorLpcdDto())
            .ForMember(dest => dest.GpsVendorId, opt => opt.MapFrom(src => src.VendorId));
    }

    private void ConfigureVendorMapping()
    {
        CreateMap<GpsVendor, VendorDto>()
            .ConstructUsing(_ => new VendorDto())
            .ForMember(dest => dest.Lpcd, opt => opt.MapFrom(src => src.Lpcds.FirstOrDefault() != null ? src.Lpcds.FirstOrDefault()!.Lpcd : string.Empty))
            .ForMember(dest => dest.RequiredAuth, opt => opt.MapFrom(src => src.GpsVendorAuth != null))
            .ForMember(dest => dest.AuthType, opt => opt.MapFrom(src => src.GpsVendorAuth != null ? src.GpsVendorAuth.AuthType : "NoAuth"))
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.GpsVendorAuth != null ? src.GpsVendorAuth.Username : null))
            .ForMember(dest => dest.Password, opt => opt.MapFrom(src => src.GpsVendorAuth != null ? src.GpsVendorAuth.Password : null))
            .ForMember(dest => dest.Endpoints, opt => opt.MapFrom(src => src.GpsVendorEndpoints))
            .ForMember(dest => dest.Auth, opt => opt.MapFrom(src => src.GpsVendorAuth))
            .ForMember(dest => dest.Mappings, opt => opt.MapFrom(src => src.Mappings))
            .ForMember(dest => dest.Lpcds, opt => opt.MapFrom(src => src.Lpcds))
            .ForMember(dest => dest.GeofenceMasterEndpoints, opt => opt.Ignore())
            .ForMember(dest => dest.GeofenceMasterAuth, opt => opt.Ignore())
            .ForMember(dest => dest.GeofenceMasterMappings, opt => opt.Ignore())
            .ForMember(dest => dest.GeofenceMasterLpcds, opt => opt.Ignore())
            .ForMember(dest => dest.LpcdCodes, opt => opt.Ignore());
    }

    private void ConfigureGeofenceMapping()
    {
        CreateMap<GeofenceDestination, GeofenceDestinationDto>()
            .ConstructUsing(_ => new GeofenceDestinationDto())
            .ForMember(dest => dest.PolygonCoordinates, opt => opt.MapFrom(src => (JsonObject?)CloneJson(src.PolygonCoordinates)));
    }
}
