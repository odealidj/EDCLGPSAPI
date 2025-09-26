using System.Text.Json;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Infrastructure.Adapters.Normalization;

// Normalizer data telemetri vendor berbasis System.Text.Json (Zero Reflection & AOT Ready)
public sealed class VendorTelemetryNormalizer : ITelemetryNormalizer
{
    private readonly ILogger<VendorTelemetryNormalizer> _logger;

    public VendorTelemetryNormalizer(ILogger<VendorTelemetryNormalizer> logger)
    {
        _logger = logger;
    }

    public List<TelemetryPoint> Normalize(
        Guid vendorId,
        string rawJson,
        string dataPath,
        IReadOnlyList<VendorFieldMapping> mappings,
        string endpointUrl)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || mappings.Count == 0)
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            var dataItems = ExtractDataItems(root, dataPath);

            var points = new List<TelemetryPoint>(dataItems.Count);

            foreach (var item in dataItems)
            {
                var point = new TelemetryPoint();

                foreach (var mapping in mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.ResponseField) || string.IsNullOrWhiteSpace(mapping.MappedField))
                    {
                        continue;
                    }

                    var val = ExtractValueByPath(item, mapping.ResponseField);
                    if (val != null)
                    {
                        TelemetryPoint.SetMappedField(point, mapping.MappedField, val);
                    }
                }

                // Normalisasi jam khusus jika diperlukan (misal vendor puninar offset +7)
                if (endpointUrl.Contains("puninarlogistics", StringComparison.OrdinalIgnoreCase))
                {
                    point.Datetime = point.Datetime.AddHours(7);
                }

                points.Add(point);
            }

            return points;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response for vendor {VendorId}", vendorId);
            return [];
        }
    }

    public List<TelemetryPoint> NormalizeCombined(
        Guid vendorId,
        IReadOnlyList<string> rawJsons,
        string keyPath,
        string dataPath,
        IReadOnlyList<VendorFieldMapping> mappings,
        string endpointUrl)
    {
        if (rawJsons.Count == 0 || mappings.Count == 0) return [];

        try
        {
            // Kumpulkan seluruh data items dari respons-respons vendor
            var mergedByKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);

            foreach (var json in rawJsons)
            {
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var items = ExtractDataItems(doc.RootElement, dataPath);

                foreach (var item in items)
                {
                    var keyValue = ExtractValueByPath(item, keyPath)?.ToString();
                    if (string.IsNullOrWhiteSpace(keyValue)) continue;

                    if (!mergedByKey.TryGetValue(keyValue, out var propDict))
                    {
                        propDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        mergedByKey[keyValue] = propDict;
                    }

                    // Kumpulkan semua field dari item ini
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            propDict[prop.Name] = ExtractJsonPrimitive(prop.Value);
                        }
                    }
                }
            }

            // Normalisasi data yang telah digabung ke TelemetryPoint
            var points = new List<TelemetryPoint>(mergedByKey.Count);
            foreach (var kvp in mergedByKey)
            {
                var point = new TelemetryPoint();
                var propDict = kvp.Value;

                foreach (var mapping in mappings)
                {
                    if (propDict.TryGetValue(mapping.ResponseField, out var val) && val != null)
                    {
                        TelemetryPoint.SetMappedField(point, mapping.MappedField, val);
                    }
                }

                if (endpointUrl.Contains("puninarlogistics", StringComparison.OrdinalIgnoreCase))
                {
                    point.Datetime = point.Datetime.AddHours(7);
                }

                points.Add(point);
            }

            return points;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to normalize combined responses for vendor {VendorId}", vendorId);
            return [];
        }
    }

    public int? ExtractMaxProperty(string rawJson, string dataPath, string maxPropertyPath)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || string.IsNullOrWhiteSpace(maxPropertyPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var items = ExtractDataItems(doc.RootElement, dataPath);
            int? maxVal = null;

            foreach (var item in items)
            {
                var valObj = ExtractValueByPath(item, maxPropertyPath);
                if (valObj != null && int.TryParse(valObj.ToString(), out var intVal))
                {
                    if (!maxVal.HasValue || intVal > maxVal.Value)
                    {
                        maxVal = intVal;
                    }
                }
            }

            return maxVal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract max property {PropertyPath}", maxPropertyPath);
            return null;
        }
    }

    private static List<JsonElement> ExtractDataItems(JsonElement root, string dataPath)
    {
        var result = new List<JsonElement>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                result.Add(el);
            }
            return result;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(dataPath) || dataPath == "." || dataPath == "/")
        {
            result.Add(root);
            return result;
        }

        var current = root;
        var parts = dataPath.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part)) continue;

            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return result;
            }
        }

        if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in current.EnumerateArray())
            {
                result.Add(el);
            }
        }
        else if (current.ValueKind == JsonValueKind.Object)
        {
            result.Add(current);
        }

        return result;
    }

    private static object? ExtractValueByPath(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var current = element;
        var parts = path.Split('.');

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part)) continue;

            // Handle segment dengan indeks array seperti items[0]
            var bracketIndex = part.IndexOf('[');
            if (bracketIndex > 0 && part.EndsWith(']'))
            {
                var propName = part[..bracketIndex];
                var indexStr = part[(bracketIndex + 1)..^1];

                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(propName, out var arrayElem) &&
                    arrayElem.ValueKind == JsonValueKind.Array && int.TryParse(indexStr, out var arrayIdx))
                {
                    int currIdx = 0;
                    bool found = false;
                    foreach (var item in arrayElem.EnumerateArray())
                    {
                        if (currIdx == arrayIdx)
                        {
                            current = item;
                            found = true;
                            break;
                        }
                        currIdx++;
                    }

                    if (!found) return null;
                    continue;
                }
                return null;
            }

            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return ExtractJsonPrimitive(current);
    }

    private static object? ExtractJsonPrimitive(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => el.GetRawText()
    };
}
