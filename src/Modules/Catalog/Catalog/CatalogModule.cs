
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;


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
            
            // Data - Infrastructure Services
            var connectionString = configuration.GetConnectionString("Database");
            
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseNpgsql(connectionString));
            
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
