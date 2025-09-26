using System.Text.Json.Serialization;

namespace Shared.Data.Mongo.DTOs;

// Request query parameters untuk filter dan paginasi data raw response asli di MongoDB
public class RawResponseFilterRequest
{
    [JsonPropertyName("vendorId")]
    public string? VendorId { get; set; }

    [JsonPropertyName("vendorName")]
    public string? VendorName { get; set; }

    [JsonPropertyName("fromDate")]
    public DateTime? FromDate { get; set; }

    [JsonPropertyName("toDate")]
    public DateTime? ToDate { get; set; }

    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;
}

// Representasi DTO data raw response asli dari vendor di MongoDB
public class RawResponseItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("vendorId")]
    public string VendorId { get; set; } = string.Empty;

    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = string.Empty;

    [JsonPropertyName("endpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    [JsonPropertyName("batchId")]
    public string? BatchId { get; set; }

    [JsonPropertyName("collectedAt")]
    public DateTime CollectedAt { get; set; }

    [JsonPropertyName("recordCount")]
    public int RecordCount { get; set; }

    [JsonPropertyName("rawResponse")]
    public object? RawResponse { get; set; }

    [JsonPropertyName("expireAt")]
    public DateTime ExpireAt { get; set; }
}
