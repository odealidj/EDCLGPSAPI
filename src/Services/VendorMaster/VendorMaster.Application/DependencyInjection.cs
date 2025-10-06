using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using VendorMaster.Application.Mappings;

namespace VendorMaster.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<VendorMappingProfile>();
        });

        return services;
    }
}
