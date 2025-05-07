using Shared.DDD;

namespace Delivery.Delivery.Models;

public class GpsDeliveryH: Aggregate<Guid>
{
    //CREATE TABLE edcl.tb_r_gps_delivery_h (
    // 	"Id" uuid NOT NULL,
    // 	"GpsVendorId" uuid NOT NULL,
    // 	"GpsVendorName" varchar(20) NOT NULL,
    // 	"DeliveryNo" varchar(20) NULL,
    // 	"NoKtp" varchar(30) NULL,
    // 	"CreatedAt" timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    // 	"CreatedBy" varchar(20) NULL,
    // 	"LastModified" timestamp NULL,
    // 	"LastModifiedBy" varchar(20) NULL,
    // 	CONSTRAINT pk_tb_r_gps_delivery_h PRIMARY KEY ("Id")
    // );
    
    public Guid GpsVendorId { get; set; }
    public string GpsVendorName { get; set; } = string.Empty;
    public string? DeliveryNo { get; set; }
    public string? NoKtp { get; set; }
    
}