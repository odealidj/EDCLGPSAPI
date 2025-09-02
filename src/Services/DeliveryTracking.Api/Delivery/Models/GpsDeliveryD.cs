using Shared.DDD;

namespace Delivery.Delivery.Models;

public class GpsDeliveryD: Entity<Guid>
{
    //CREATE TABLE edcl.tb_r_gps_delivery_d (
    // 	"Id" uuid NOT NULL,
    // 	"GpsDeliveryHId" uuid,
    // 	"LpcdId" uuid NULL,
    // 	"Lpcd" varchar(5) NULL,
    // 	"PlatNo" varchar(50) NULL,
    // 	"DeviceId" varchar(100) NULL,
    // 	"Datetime" timestamp NULL,
    // 	"X" numeric(19, 16) NULL,
    // 	"Y" numeric(19, 16) NULL,
    // 	"Speed" numeric(5) NULL,
    // 	"Course" numeric(5) NULL,
    // 	"StreetName" varchar(500) NULL,
    // 	"CreatedAt" timestamp NULL,
    // 	"CreatedBy" varchar(20) NULL,
    // 	"LastModified" timestamp NULL,
    // 	"LastModifiedBy" varchar(20) NULL,
    // 	CONSTRAINT pk_tb_r_gps_delivery_d PRIMARY KEY ("Id")
    // );
    
    public Guid? GpsDeliveryHId { get; set; }
    public Guid? LpcdId { get; set; }
    public string? Lpcd { get; set; }   
    public string? PlatNo { get; set; }
    public string? DeviceId { get; set; }
    public DateTime? Datetime { get; set; }
    public decimal? X { get; set; }
    public decimal? Y { get; set; }
    public decimal? Speed { get; set; }
    public decimal? Course { get; set; }
    public string? StreetName { get; set; }
}