namespace GeofenceMaster.GeofenceMaster.Models;

// create table tb_m_system (
// 	"SysCat" varchar(30),
// 	"SysSubCat" varchar(255),
// 	"SysCd" varchar(30),
// 	"SysValue" varchar(255),
// 	"Remarks" varchar(100),
// 	CONSTRAINT PK_TB_M_SYSTEM PRIMARY KEY ("SysCat","SysSubCat","SysCd")
// )

public class Msystem
{
    public string SysCat { get; set; } = string.Empty;
    public string SysSubCat { get; set; } = string.Empty;
    public string SysCd { get; set; } = string.Empty;
    public string SysValue { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    
    public DateTime? CreatedAt { get; set; } 
    public string? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; } 
    public string? LastModifiedBy { get; set; }

    public Msystem(string sysCat, string sysSubCat, string sysCd, string sysValue, string remarks,
        DateTime? createdAt, string? createdBy, DateTime? lastModified, string? lastModifiedBy)
    {
        SysCat = sysCat;
        SysSubCat = sysSubCat;
        SysCd = sysCd;
        SysValue = sysValue;
        Remarks = remarks;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        LastModified = lastModified;
        LastModifiedBy = lastModifiedBy;
    }
}