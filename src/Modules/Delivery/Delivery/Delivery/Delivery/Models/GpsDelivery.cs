namespace Delivery.Delivery.Models;

public class GpsDelivery: Entity<Guid>
{
    /*
       CREATE TABLE edcl.tb_r_gps_delivery (
        "Id" uuid NOT NULL,
        "GpsVendorId" uuid NOT NULL,
        "GpsVendorName" varchar(20) NOT NULL,
        "GpsDeliveryHId" uuid NULL,
        "LpcdId" uuid NULL,
        "Lpcd" varchar(5) NULL,
        "DeliveryNo" varchar(20) NULL,
        "NoKtp" varchar(30) NULL,
        "PlatNo" varchar(50) NULL,
        "DeviceId" varchar(100) NULL,
        "Datetime" timestamp NULL,
        "X" numeric(19, 16) NULL,
        "Y" numeric(19, 16) NULL,
        "Speed" numeric(5) NULL,
        "Course" numeric(5) NULL,
        "StreetName" varchar(500) NULL,
        "CreatedAt" timestamp DEFAULT CURRENT_TIMESTAMP NULL,
        "CreatedBy" varchar(20) NULL,
        "LastModified" timestamp NULL,
        "LastModifiedBy" varchar(20) NULL,
        CONSTRAINT pk_tb_r_gps_delivery PRIMARY KEY ("Id")
       );

    */
    public Guid GpsVendorId { get; set; } 
    public string GpsVendorName { get; set; } = string.Empty;
    public Guid GpsDeliveryHId { get; set; }
    public Guid? LpcdId { get; set; }
    public string? Lpcd { get; set; } = string.Empty;
    public string? DeliveryNo { get; set; } = string.Empty;
    public string? NoKtp { get; set; } = string.Empty;
    public string? PlatNo { get; set; } = string.Empty;
    public string? DeviceId { get; set; } = string.Empty;
    public DateTime? Datetime { get; set; } 
    public decimal? X { get; set; } 
    public decimal? Y { get; set; } 
    public decimal? Speed { get; set; } 
    public decimal ?Course { get; set; } 
    public string? StreetName { get; set; } = string.Empty;
    
}