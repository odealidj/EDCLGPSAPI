using AutoMapper;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Domain.Entities;

namespace DeliveryTracking.Application.Mappings;

/// <summary>
/// AutoMapper profile untuk transformasi entitas domain DeliveryTracking ke DTO.
/// Mengikuti best practice Clean Architecture & CQRS.
/// </summary>
public class DeliveryTrackingMappingProfile : Profile
{
    public DeliveryTrackingMappingProfile()
    {
        // 1. DeliveryProgress -> DeliveryProgressDto
        CreateMap<DeliveryProgress, DeliveryProgressDto>()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.LastModified, opt => opt.Ignore())
            .ForMember(dest => dest.LastModifiedBy, opt => opt.Ignore());

        // 2. DeliveryTrackingSession -> DeliveryTrackingSessionDto
        CreateMap<DeliveryTrackingSession, DeliveryTrackingSessionDto>()
            .ForCtorParam(nameof(DeliveryTrackingSessionDto.Key), opt => opt.MapFrom(src => src.MatchKey))
            .ForCtorParam(nameof(DeliveryTrackingSessionDto.Val), opt => opt.MapFrom(src => src.MatchValue))
            .ForCtorParam(nameof(DeliveryTrackingSessionDto.CleanValue), opt => opt.MapFrom(src => src.CleanMatchValue))
            .ForCtorParam(nameof(DeliveryTrackingSessionDto.VendorName), opt => opt.MapFrom<string?>(_ => null));
    }
}
