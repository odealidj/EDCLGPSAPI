using Microsoft.AspNetCore.Hosting;

namespace VendorMaster.IntegrationTests.Fixtures;

// factory buat boot web application factory + postgres testcontainer
public class VendorMasterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("edclgps_integration_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        // nyalakan container postgres test
        await _dbContainer.StartAsync();

        // create schema & tabel di database postgres test
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorMasterDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS edcl;");
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // pasang connection string container test sebelum app/module di-build
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("Database", _dbContainer.GetConnectionString());
    }

    // helper buat dapetin scope db context pas butuh assert langsung ke database
    public VendorMasterDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<VendorMasterDbContext>();
    }
}
