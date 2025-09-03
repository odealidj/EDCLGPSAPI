namespace GeofenceWorker.Workers.Models;

public class GpsApiLog: Entity<Guid>
{
    // CREATE TABLE edcl.tb_m_gps_api_log (
    // 	"Id" uuid NOT NULL,
    // 	"FunctionName" varchar(255),
    // 	"Status" varchar(50),
    // 	"ErrorMessage" text,
    // 	"Parameter" text,
    // 	"Username" varchar(100),
    // 	"CreatedAt" timestamp DEFAULT CURRENT_TIMESTAMP,
    // 	"CreatedBy" varchar(20),
    // 	"LastModified" timestamp,
    // 	"LastModifiedBy" varchar(20),
    // 	CONSTRAINT pk_tb_m_gps_api_log PRIMARY KEY ("Id")
    // );
    
    public GpsApiLog(){}
    
    public GpsApiLog(Guid id, string? functionName, string? status, string? errorMessage = null,
        string? errorResponse = null,
        string? parameter = null, string? username = null)
    {
        Id = id;
        FunctionName = functionName;
        Status = status;
        ErrorMessage = errorMessage;
        ErrorResponse = errorResponse;
        Parameter = parameter;
        Username = username;
    }

    public GpsApiLog(Guid id, string? functionName, string? status, string? errorMessage = null,
        string? errorResponse = null,
        string? parameter = null, string? username = null, DateTime? createdAt = null, string? createdBy = "")
    {
        Id = id;
        FunctionName = functionName;
        Status = status;
        ErrorMessage = errorMessage;
        ErrorResponse = errorResponse;
        Parameter = parameter;
        Username = username;
        CreatedAt = createdAt ?? DateTime.Now;
        CreatedBy = createdBy ?? "System";
    }
        
    public string? FunctionName { get; set; } = string.Empty;
    public string? Status { get; set; } = string.Empty; // Success, Failed, etc.
    public string? ErrorMessage { get; set; } // Pesan error jika ada
    public string? ErrorResponse { get; set; }

    public string? Parameter { get; set; } // Parameter yang digunakan dalam fungsi
    public string? Username { get; set; } // Nama pengguna yang menjalankan fungsi
    
}