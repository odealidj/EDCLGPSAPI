using System.Text.Json.Serialization;

namespace DeliveryTracking.Application.DTOs;

// request parameter untuk filter kueri posisi kendaraan (aot-ready record)
public record LastPositionRequest(
    Guid? GpsVendorId = null,
    string? VendorName = null,
    string? PlatNo = null,
    string? DeviceId = null,
    DateTime? Datetime = null,
    DateTime? CreatedAt = null,
    int PageIndex = 1,
    int PageSize = 100
);

// dto representasi snapshot posisi gps kendaraan terakhir
public class LastPositionDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? PlatNo { get; set; }
    public string? DeviceId { get; set; }
    public DateTime Datetime { get; set; }
    public decimal? X { get; set; }
    public decimal? Y { get; set; }
    public decimal? Speed { get; set; }
    public decimal? Course { get; set; }
    public string? StreetName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string DataSource { get; set; } = "PostgreSQL"; // "Redis" atau "PostgreSQL"
}

// respon paginasi untuk daftar posisi kendaraan
public record PaginatedPositionResponse
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public long Count { get; init; }
    public bool Next { get; init; }
    public bool Prev { get; init; }
    public IReadOnlyList<LastPositionDto> Data { get; init; } = Array.Empty<LastPositionDto>();

    public PaginatedPositionResponse() { }

    [JsonConstructor]
    public PaginatedPositionResponse(
        int PageIndex,
        int PageSize,
        long Count,
        bool Next,
        bool Prev,
        IReadOnlyList<LastPositionDto> Data)
    {
        this.PageIndex = PageIndex < 1 ? 1 : PageIndex;
        this.PageSize = PageSize;
        this.Count = Count;
        this.Next = Next;
        this.Prev = Prev;
        this.Data = Data ?? Array.Empty<LastPositionDto>();
    }

    public PaginatedPositionResponse(
        int PageIndex,
        int PageSize,
        long Count,
        IReadOnlyList<LastPositionDto> Data)
        : this(
            PageIndex,
            PageSize,
            Count,
            (long)(PageIndex < 1 ? 1 : PageIndex) * PageSize < Count,
            (PageIndex < 1 ? 1 : PageIndex) > 1,
            Data)
    {
    }
}
