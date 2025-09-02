using System.Text.Json.Serialization;

namespace Delivery.Delivery.Dtos;

public class DeliveryProgressDto
{
    //CREATE TABLE edcl.tb_r_delivery_progress (
    // 	"Id" uuid,
    // 	"DeliveryNo" varchar(20) NOT NULL,
    // 	"PlatNo" varchar(20) NULL,
    // 	"NoKtp" varchar(25) NULL,
    // 	"VendorName" varchar(150) NULL,
    // 	"Lpcd" Varchar(12),
    // 	"CreatedAt" timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    // 	"CreatedBy" varchar(20) NULL,
    // 	"LastModified" timestamp NULL,
    // 	"LastModifiedBy" varchar(20) NULL,
    // 	CONSTRAINT pk_tb_r_delivery_progress PRIMARY KEY ("Id")
    // );
    
    public Guid? Id { get; set; }
    public string DeliveryNo { get; set; } 
    public string PlatNo { get; set; } = string.Empty;
    public string NoKtp { get; set; } = string.Empty;

    [JsonPropertyName("gpsVendor")] public string? VendorName { get; set; } = null;
    public string? Lpcd { get; set; }
    
}