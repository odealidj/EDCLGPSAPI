namespace GeofenceWorker.Workers.Models;

public class Msystem
{
    public string SysCat { get; set; } = string.Empty;
    public string SysSubCat { get; set; } = string.Empty;
    public string SysCd { get; set; } = string.Empty;
    public string SysValue { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    public Msystem(string sysCat, string sysSubCat, string sysCd, string sysValue, string remarks)
    {
        SysCat = sysCat;
        SysSubCat = sysSubCat;
        SysCd = sysCd;
        SysValue = sysValue;
        Remarks = remarks;
    }
}