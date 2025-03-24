using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
