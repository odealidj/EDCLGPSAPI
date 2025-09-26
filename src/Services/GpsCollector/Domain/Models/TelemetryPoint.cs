using System.Globalization;

namespace GpsCollector.Domain.Models;

// Titik telemetri gps yang ternormalisasi (zero reflection & aot ready)
public sealed class TelemetryPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HeaderId { get; set; }
    public string? Lpcd { get; set; }
    public string? PlatNo { get; set; }
    public string? DeviceId { get; set; }
    public DateTime Datetime { get; set; } = DateTime.UtcNow;
    public decimal? X { get; set; } // Longitude
    public decimal? Y { get; set; } // Latitude
    public decimal? Speed { get; set; }
    public decimal? Course { get; set; }
    public string? StreetName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";

    // Set field dinamis tanpa refleksi (AOT friendly static switch)
    public static void SetMappedField(TelemetryPoint point, string mappedField, object? value)
    {
        if (value == null) return;

        switch (mappedField.ToUpperInvariant())
        {
            case "PLATNO":
            case "PLAT_NO":
            case "VEHICLENO":
                point.PlatNo = value.ToString()?.Trim().ToUpperInvariant();
                break;

            case "DATETIME":
            case "GPS_TIME":
            case "TIMESTAMP":
                point.Datetime = ParseDateTime(value);
                break;

            case "X":
            case "LONGITUDE":
            case "LNG":
            case "LON":
                point.X = ParseDecimal(value);
                break;

            case "Y":
            case "LATITUDE":
            case "LAT":
                point.Y = ParseDecimal(value);
                break;

            case "SPEED":
                point.Speed = ParseDecimal(value);
                break;

            case "COURSE":
            case "HEADING":
            case "DIRECTION":
                point.Course = ParseDecimal(value);
                break;

            case "STREETNAME":
            case "STREET_NAME":
            case "ADDRESS":
            case "LOCATION":
                point.StreetName = value.ToString();
                break;

            case "DEVICEID":
            case "DEVICE_ID":
            case "TRUCKID":
            case "IMEI":
                point.DeviceId = value.ToString();
                break;

            case "LPCD":
                point.Lpcd = value.ToString();
                break;
        }
    }

    public static DateTime ParseDateTime(object? value)
    {
        if (value is DateTime dt) return dt;
        if (value is DateTimeOffset dto) return dto.UtcDateTime;

        var str = value?.ToString();
        if (string.IsNullOrWhiteSpace(str)) return DateTime.UtcNow;

        if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedOffset))
        {
            return parsedOffset.UtcDateTime;
        }

        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt))
        {
            return DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    public static decimal? ParseDecimal(object? value)
    {
        if (value == null) return null;
        if (value is decimal d) return d;
        if (value is double dbl) return Convert.ToDecimal(dbl);
        if (value is float f) return Convert.ToDecimal(f);
        if (value is int i) return i;
        if (value is long l) return l;

        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str)) return null;

        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
