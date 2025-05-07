using System.Reflection;
using Delivery.Delivery.Models;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Data;

public class DeliveryDbContext: DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options) { }
    
    public DbSet<GpsDeliveryH> GpsDeliveryHs => Set<GpsDeliveryH>();
    public DbSet<GpsDeliveryD> GpsDeliveryDs => Set<GpsDeliveryD>();
    public DbSet<DeliveryProgress> DeliveryProgresses => Set<DeliveryProgress>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");
        
        builder.Entity<GpsDeliveryH>().ToTable("tb_r_gps_delivery_h");
        builder.Entity<GpsDeliveryD>().ToTable("tb_r_gps_delivery_d");
        builder.Entity<DeliveryProgress>().ToTable("tb_r_delivery_progress");
        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);

    }
    
    
}