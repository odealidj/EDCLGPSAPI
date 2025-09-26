namespace VendorMaster.Domain.Entities;

// parameter konfigurasi sistem (tabel m_system)
public class Msystem
{
    public string SysCat { get; set; } = string.Empty;
    public string SysSubCat { get; set; } = string.Empty;
    public string SysCd { get; set; } = string.Empty;
    public string SysValue { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
}
