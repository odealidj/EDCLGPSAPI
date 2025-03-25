using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GpsInterface
{
    public static class GpsInterfaceModule
    {
        public static IServiceCollection AddGpsInterfaceModule(this IServiceCollection services,
            IConfiguration configuration)
        {
            return services;
        }

        public static IApplicationBuilder UseGpsInterfaceModule(this IApplicationBuilder app)
        {
            return app;
        }
    }
}
