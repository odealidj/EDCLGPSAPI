namespace GeofenceWorker.Workers.Models;

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