
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Data.Interceptors;

namespace Catalog
{
    public static class CatalogModule
    {
        public static IServiceCollection AddCatalogModule(this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add service to the container
            
            // Api endppoint services
            
            // Application Use Case Services
            // perlu di register karena pada primary constructor DispatchDomainEventsInterceptor mneg inject IMediator
            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            });
            
            // Data - Infrastructure Services
            var connectionString = configuration.GetConnectionString("Database");
            
            services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
            services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
            
            services.AddDbContext<CatalogDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());  // get services of type ISaveChangesInterceptor
                options.UseNpgsql(connectionString);
            });
            
            services.AddScoped<IDataSeeder, CatalogDataSeeder>();
            
            return services;
        }
        
        public static IApplicationBuilder UseCatalogModule(this IApplicationBuilder app)
        {
            // Configure the HTTP request pipeline
            
            // 1. Use API endpoint services
            
            // 2. Use Application Use Case Services
            
            // 3. Use Data - Infrastructure Services

            app.UseMigration<CatalogDbContext>();
            
            return app;
        }


    }
}
