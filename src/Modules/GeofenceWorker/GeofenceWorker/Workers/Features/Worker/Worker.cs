using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Helper;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Workers.Dtos;
using GeofenceWorker.Workers.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GeofenceWorker.Workers.Features.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    ////private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory; 
    ///private readonly IBus _bus;
    private readonly IRabbitMqService _rabbitMqService;
    ////private readonly IPublishEndpoint _publishEndpoint;
    public Worker( IServiceScopeFactory scopeFactory, 
        ////IBus bus,
        /////IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        ////HttpClient httpClient, 
        IRabbitMqService rabbitMqService,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        /////_httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        ///_bus = bus; 
        _rabbitMqService = rabbitMqService;
        ////_publishEndpoint = publishEndpoint;
        _logger = logger;
        
        
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var durationTime = "1"; // Default ke 1 menit jika tidak ada nilai di database
                
                using (var scope = _scopeFactory.CreateScope()) // Create a scope for DbContext
                {
                    var context = scope.ServiceProvider.GetRequiredService<GeofenceWorkerDbContext>();
                    
                    durationTime = await context.Msystems
                        .Where(x => x.SysCat == "Delay" && x.SysSubCat == "EDCL_GPS_WORKER_SERVICE" && x.SysCd == "1")
                        .Select(x => x.SysValue)
                        .FirstOrDefaultAsync(stoppingToken);
                    
                    // Get all vendors that need to call endpoints
                    var vendors = await context.GpsVendors
                        .Where(x => x.Id == Guid.Parse("17fb5f39-3de5-4c09-88fe-5f15e245f186"))
                        .Include(v => v.Auth)
                        .ToListAsync(stoppingToken);

                    foreach (var vendor
                             in vendors)
                    {
                        try
                        {                        
                            var endpoints = await context.GpsVendorEndpoints
                                .Where(x => x.GpsVendorId == vendor.Id)
                                .ToListAsync(stoppingToken);

                            if (vendor.ProcessingStrategy?.ToLowerInvariant() == "combined" && endpoints.Count > 0)
                            {
                                // Proses endpoint secara gabungan
                                await ProcessCombinedEndpoints(vendor, endpoints, scope, context, stoppingToken);
                            }
                            else
                            {
                                // Proses setiap endpoint secara individual
                                foreach (var endpoint in endpoints)
                                {
                                    await ProcessIndividualEndPoint(endpoint, scope, context, stoppingToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing vendor {VendorName}: {Message}", vendor.VendorName, ex.Message);
                        }
                    }
                }
                
                // Wait for 1 minute (60,000 milliseconds) before next cycle
                
                //await Task.Delay(60000, stoppingToken); // 1 minute delay
                
                if (!int.TryParse(durationTime, out var delayTime))
                {
                    delayTime = 1; // Default ke 1 menit jika tidak ada nilai di database
                }
                
                await Task.Delay(TimeSpan.FromMinutes(delayTime), stoppingToken); 
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred in the worker: {Message}", e.Message);
        }
    }

    private async Task ProcessCombinedEndpoints(GpsVendor vendor, List<GpsVendorEndpoint> endpoints, IServiceScope scope,
        GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(vendor.ProcessingStrategyPathKey);
        
        var client = _httpClientFactory.CreateClient();
        
        var responses = new List<string>();
        
        foreach (var endpoint in endpoints)
        {
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod(endpoint.Method),
                RequestUri = new Uri(endpoint.BaseUrl),
                Content =  endpoint.Bodies != null?  new StringContent(endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json"): null
            };
            
            // Add Headers from JsonObject if any
            if (endpoint.Headers != null)
            {
                foreach (var header in endpoint.Headers.AsObject())
                {
                    // Add each header from the JsonObject to the request headers
                    request.Headers.Add(header.Key, header.Value?.ToString());
                }
            }
        
            // Attach parameters to the URL if any
            if (endpoint.Params != null || endpoint.VarParams != null)
            {
                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

                if (endpoint.Params != null)
                {
                    foreach (var param in endpoint.Params.AsObject())
                    {
                        query[param.Key] = param.Value?.ToString();
                    }
                }

                if (endpoint.VarParams != null)
                {
                    foreach (var param in endpoint.VarParams.AsObject())
                    {
                        query[param.Key] = param.Value?.ToString();
                    }
                }

                request.RequestUri = new UriBuilder(request.RequestUri)
                {
                    Query = query.ToString()
                }.Uri;
            }
        
            // Set Authorization Header if required
            if (endpoint.GpsVendor is { RequiredAuth: true })
            {
                if (endpoint.GpsVendor.AuthType == "Basic")
                {
                    if (string.IsNullOrEmpty(endpoint.GpsVendor.Username)) ArgumentException.ThrowIfNullOrEmpty(nameof(endpoint.GpsVendor.Username));
                
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{endpoint.GpsVendor.Username}:{endpoint.GpsVendor.Password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authValue);
                }
                else if (endpoint.GpsVendor is { AuthType: "Bearer", Auth: not null })
                {
                    var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                    if (string.IsNullOrEmpty(authToken)) continue;
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authToken);
                }
            }

            //JsonDocument? responseData = null;
            try
            {

                var response = await client.SendAsync(request, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseData =
                        await response.Content.ReadAsStringAsync(stoppingToken);

                    responses.Add(responseData);
                    //var dataMapping = GetDataItems(responseData, "vin");
                }
                else
                {
                    _logger.LogError(
                        "Failed to call endpoint for Vendor {VendorName} - {BaseUrl}: {StatusCode} {ReasonPhrase}",
                        vendor.VendorName, endpoint.BaseUrl, response.StatusCode, response.ReasonPhrase);
                    
                    var responseData =
                        await response.Content.ReadAsStringAsync(stoppingToken);
                    
                    using (var scopeGpsApiLog = _scopeFactory.CreateScope()) 
                    {
                        // GpsApiLog(Guid id, string? functionName, string? status, string? errorMessage = null, 
                        // string? parameter = null, string? username = null)
                
                        var iGpsApiLogRepository = scopeGpsApiLog.ServiceProvider.GetRequiredService<IGpsApiLogRepository>();
                        await iGpsApiLogRepository.InsertGpsApiLog(
                            new GpsApiLog(
                                Guid.NewGuid(), 
                                response.RequestMessage?.RequestUri?.ToString(),
                                "0",
                                response.ReasonPhrase,
                                responseData,
                                endpoint.GpsVendor.VendorName,
                                "",
                                DateTime.UtcNow,
                                "System"
                            ), stoppingToken);
                    }
                    
                    return; // Hentikan pemrosesan jika salah satu endpoint gagal (opsional, bisa disesuaikan)

                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calling endpoint for Vendor {VendorName} - {BaseUrl}: {ErrorMessage}",
                    vendor.VendorName, endpoint.BaseUrl, ex.Message);
                return;
            }
        }
        
        if (responses.Count != 0)
        {
            var combinedDataList = CombineJsonToString(responses, vendor.ProcessingStrategyPathKey??"", vendor.ProcessingStrategyPathData??"data");
            
            var lastPositionDs = await ProcessMappingResponse(vendor.Id ,combinedDataList, vendor.ProcessingStrategyPathData??"data", context, endpoints.First().BaseUrl);
            
            await CreateNewGpsLastPosition(vendor, lastPositionDs, stoppingToken);

            await PublishGpsMessageAsync(endpoints.First(), lastPositionDs);
            
            _logger.LogInformation($"Vendor: {vendor.VendorName }");
        }
        else
        {
            _logger.LogWarning($"Tidak ada data untuk digabungkan untuk vendor: {vendor.VendorName}.");
        }
        
        
        /*
        if (responses.Count == 2)
        {
            var mergedData = MergeResponses(vendor, responses);
            if (mergedData != null)
            {
                // Proses data yang digabungkan
                await ProcessMergedData(vendor, mergedData, scope, context, stoppingToken);
            }
        }
        */
        
    }

    private async Task ProcessIndividualEndPoint(GpsVendorEndpoint endpoint, IServiceScope scope, GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(endpoint.Method),
            RequestUri = new Uri(endpoint.BaseUrl),
            //Content =  endpoint.Bodies != null?  new StringContent(endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json"): null
        };

        if (endpoint.Bodies != null)
        {
            request.Content = new StringContent(endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json");
        }
        else
        {
            request.Content = null;
        }
        
        // Add Headers from JsonObject if any
        if (endpoint.Headers != null)
        {
            foreach (var header in endpoint.Headers.AsObject())
            {
                // Add each header from the JsonObject to the request headers
                request.Headers.Add(header.Key, header.Value?.ToString());
            }
        }
        
        // Attach parameters to the URL if any
        if (endpoint.Params != null || endpoint.VarParams != null)
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

            if (endpoint.Params != null)
            {
                foreach (var param in endpoint.Params.AsObject())
                {
                    query[param.Key] = param.Value?.ToString();
                }
            }

            if (endpoint.VarParams != null)
            {
                foreach (var param in endpoint.VarParams.AsObject())
                {
                    query[param.Key] = param.Value?.ToString();
                }
            }

            request.RequestUri = new UriBuilder(request.RequestUri)
            {
                Query = query.ToString()
            }.Uri;
        }

        // Set Authorization Header if required
        if (endpoint.GpsVendor is { RequiredAuth: true })
        {
            if (endpoint.GpsVendor.AuthType == "Basic")
            {
                if (string.IsNullOrEmpty(endpoint.GpsVendor.Username)) ArgumentException.ThrowIfNullOrEmpty(nameof(endpoint.GpsVendor.Username));
                
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{endpoint.GpsVendor.Username}:{endpoint.GpsVendor.Password}"));
                
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authValue);
            }
            else if (endpoint.GpsVendor is { AuthType: "Bearer", Auth: not null })
            {
                var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                if (string.IsNullOrEmpty(authToken)) return;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authToken);
                    
            }
        }
        
        var response = await client.SendAsync(request, stoppingToken);
        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync(stoppingToken);

            var lastPositionDs = await ProcessMappingResponse(
                endpoint.GpsVendor.Id,
                responseData, 
                endpoint.GpsVendor.ProcessingStrategyPathData??"data",
                context,endpoint.BaseUrl);

            var maxData = 0;
            
            if (lastPositionDs.Count > 0)
            {
                var geofenceMaster = await CreateNewGpsLastPosition(endpoint.GpsVendor, lastPositionDs, stoppingToken);
                
                if (!string.IsNullOrEmpty(endpoint.MaxPath))
                {
                    var dataItens = await GetDataItems(responseData, endpoint.GpsVendor.ProcessingStrategyPathData??"data");
                    maxData = await FindMaxProperty.FindMaxPropertyValueWithExceptionAsync<int>(dataItens, endpoint.MaxPath);
                    await UpdateLastPositionId(endpoint, endpoint.MaxPath, maxData);
                }
                
                await PublishGpsMessageAsync(endpoint, lastPositionDs);
               
            }

            _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}", endpoint.GpsVendor.VendorName);
            
        }
        else
        {
            var responseData = await response.Content.ReadAsStringAsync(stoppingToken);

            using (var scopeGpsApiLog = _scopeFactory.CreateScope()) 
            {
                // GpsApiLog(Guid id, string? functionName, string? status, string? errorMessage = null, 
                // string? parameter = null, string? username = null)
                
                var iGpsApiLogRepository = scopeGpsApiLog.ServiceProvider.GetRequiredService<IGpsApiLogRepository>();
                await iGpsApiLogRepository.InsertGpsApiLog(
                    new GpsApiLog(
                        Guid.NewGuid(), 
                        response.RequestMessage?.RequestUri?.ToString(),
                        "0",
                        response.ReasonPhrase,
                        responseData,
                        endpoint.GpsVendor.VendorName,
                        "",
                        DateTime.UtcNow,
                        "System"
                    ), stoppingToken);
            }

            _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}, Data: {responseData}", endpoint.GpsVendor?.VendorName, response.StatusCode, response.ReasonPhrase, responseData);
        }
    }
    
    private async Task PublishGpsMessageAsync(GpsVendorEndpoint endpoint, IList<GpsLastPositionD> lastPositionDs)
    {
        
        string routingKey = $"gps.vendor.{endpoint.GpsVendor.Id}";
        var message = CreateGpsMessage(endpoint.GpsVendor, lastPositionDs.ToList());
        
        await _rabbitMqService.PublishAsync(message, routingKey);
    }

    private async Task UpdateLastPositionId(GpsVendorEndpoint endpoint, string properti, int? newLastPositionId)
    {
        if (newLastPositionId == null)
        {
            throw new ArgumentNullException(nameof(newLastPositionId), "newLastPositionId cannot be null.");
        }
        
        var updatedVarParams = false;
        
        if (endpoint.VarParams != null)
        {
            if (endpoint.VarParams is JsonNode varParamsNode and JsonArray varParamsArray)
            {
                foreach (var element in varParamsArray)
                {
                    if (element is JsonObject paramSet && paramSet.ContainsKey(properti))
                    {
                        paramSet[properti] = newLastPositionId;
                        break; // Asumsi: hanya update properti pertama yang ditemukan
                    }
                }
            }
            //else if (endpoint.VarParams is JsonObject varParamsObject && varParamsObject.ContainsKey(properti))
            else if (endpoint.VarParams is { } varParamsObject && varParamsObject.ContainsKey(properti))
            {
                varParamsObject[properti] = newLastPositionId;

            }

            updatedVarParams = true;
        }

        if (updatedVarParams)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
            await repository.UpdateVarParamsPropertyRawSqlAsync(
                endpoint.Id, 
                properti,
                newLastPositionId,
                DateTime.UtcNow,
                "System");
        }

    }
    
    private async Task<List<JToken>> GetDataItems(string jsonResponse, string dataPath)
    {
        return await Task.Run(() =>
        {
            var token = JToken.Parse(jsonResponse);
            List<JToken> dataItems = new List<JToken>();

            if (token is JArray rootArray)
            {
                dataItems = rootArray.Children().ToList();
            }
            else if (token is JObject rootObject)
            {
                var dataToken = rootObject.SelectToken(dataPath);
                if (dataToken is JArray dataArray)
                {
                    dataItems = dataArray.Children().ToList();
                }
                else if (dataToken is JObject dataObject)
                {
                    // Jika properti data adalah objek, konversikan menjadi array dengan satu elemen
                    dataItems = new List<JToken> { dataObject };
                }
                else if (dataToken != null)
                {
                    // Jika properti data adalah nilai primitif, konversikan menjadi array dengan satu elemen
                    dataItems = new List<JToken> { dataToken };
                }
                else
                {
                    dataItems = new List<JToken>(); // Jika dataPath tidak ditemukan
                }
            }

            return dataItems;
        });
    }
    
    private static List<Dictionary<string, object>> CombineJson(List<string> jsonResponses, string key, string dataPath = "data")
    {
        if (jsonResponses == null || !jsonResponses.Any())
        {
            return new List<Dictionary<string, object>>();
        }

        var allDataItems = new List<Dictionary<string, object>>();

        foreach (var jsonResponse in jsonResponses)
        {
            try
            {
                var document = JsonDocument.Parse(jsonResponse);
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(dataPath, out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    var dataItems = dataElement.Deserialize<List<Dictionary<string, object>>>();
                    if (dataItems != null)
                    {
                        allDataItems.AddRange(dataItems);
                    }
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Array) // Handle if the root is directly the data array
                {
                    var dataItems = document.Deserialize<List<Dictionary<string, object>>>();
                    if (dataItems != null)
                    {
                        allDataItems.AddRange(dataItems);
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        if (!allDataItems.Any())
        {
            return new List<Dictionary<string, object>>();
        }

        var combinedDataList = new List<Dictionary<string, object>>();
        var groupedByVin = allDataItems.GroupBy(item => item.ContainsKey(key) ? item[key]?.ToString() : null).Where(g => g.Key != null);

        foreach (var group in groupedByVin)
        {
            var combinedItem = new Dictionary<string, object>();
            foreach (var item in group)
            {
                foreach (var prop in item)
                {
                    if (!combinedItem.ContainsKey(prop.Key))
                    {
                        combinedItem[prop.Key] = prop.Value;
                    }
                }
            }
            combinedDataList.Add(combinedItem);
        }

        return combinedDataList;
    }
    
    private static string CombineJsonToString(List<string> jsonResponses, string key, string dataPath, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var combinedList = CombineJson(jsonResponses, key, dataPath);
        return JsonSerializer.Serialize(combinedList, jsonSerializerOptions);
    }
    
    private async Task<IList<GpsLastPositionD>> ProcessMappingResponse(Guid vendorId, string jsonResponse, string dataPath, GeofenceWorkerDbContext _context, string endpointName)
    {
        
        // 1. Ambil semua mapping untuk vendor
        var mappings = await _context.Mappings
            .Where(v => v.GpsVendorId == vendorId)
            .AsNoTracking()
            .ToListAsync();

        if (mappings.Count == 0) return [];
        var  dataItems = await GetDataItems(jsonResponse, dataPath);
        //var  dataItems = GetDataItems(jsonResponse, mappings.First().DataPath ?? string.Empty);

        var gpsLastPositions = new List<GpsLastPositionD>();

        foreach (var dataItem in dataItems)
        {
            var gpsLastPosition = new GpsLastPositionD
            {
                Id = Guid.NewGuid()
            };

            foreach (var mapping in mappings)
            {
                try
                {
                    // 4. Ekstrak nilai dari JSON
                    var valueToken = dataItem.SelectToken(mapping.ResponseField);
                    if (valueToken == null) continue;

                    // 5. Set properti di VehicleData menggunakan refleksi
                    PropertyInfo? property = typeof(GpsLastPositionD).GetProperty(mapping.MappedField);
                    if (property == null) continue;

                    object? value;
                    Type propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (propType == typeof(DateTime))
                    {
                        if (valueToken.Type == JTokenType.Null)
                        {
                            value = null;
                        }
                        else
                        {
                            //var  value1 = valueToken.ToObject(property.PropertyType);
                            
                            var dateTimeValue = valueToken.ToObject<DateTime>();
                            
                            /////var local = ParseAndAdjustToUtcPlus7(dateTimeValue.ToString(CultureInfo.CurrentCulture));
                            ////var local = ParseDateTimeWithOffset(value1.ToString());
                            
                            ////var local = ParseAndAdjustToUtcPlus7(dateTimeValue.ToString("o", CultureInfo.InvariantCulture));
                            
                            //var local = ParseToJakartaLocal(dateTimeValue.ToString("o", CultureInfo.InvariantCulture));
                            /////var local = ParseToJakartaLocal(dateTimeValue.ToString(CultureInfo.CurrentCulture));
                            
                            
                            ////var dateTimeValue = valueToken.ToObject<DateTime>();
                            ////TimeZoneInfo jakartaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
                            ////DateTime localDateTime = TimeZoneInfo.ConvertTimeFromUtc(local, jakartaTimeZone);
                            
                            
                            /*
                            dateTimeValue = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc);
                            //dateTimeValue = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified);
                            TimeZoneInfo jakartaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
                            DateTime localDateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeValue, jakartaTimeZone);
                            */

                            var local = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified);
                            if (endpointName.ToLower() == "https://tnt-micro.puninarlogistics.com/api/tracking-tmmin".ToLower())
                            {
                                local = local.AddHours(7);
                            }
                            value = local;

                            /*
                            value = dateTimeValue.Kind == DateTimeKind.Utc
                                ? DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified)
                                : dateTimeValue;
                            */

                            //value = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified);


                            //Datetime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) 

                            ////value = dateTimeValue;

                        }

                        // Jika properti nullable, konversi ke tipe nullable
                        if (property.PropertyType != propType)
                        {
                            value = Activator.CreateInstance(property.PropertyType, value);
                        }
                    }
                    else
                    {
                        value = valueToken.ToObject(property.PropertyType);
                    }
                    
                    property.SetValue(gpsLastPosition, value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error mapping {mapping.MappedField}: {ex.Message}");
                }
            }

            gpsLastPositions.Add(gpsLastPosition);
        }

        return gpsLastPositions;
    }
    
    public static DateTime ParseDateTimeWithOffset(string dateTimeString)
    {
        // Format datetime dengan offset
        string[] formatsWithOffset = {
            "yyyy-MM-dd'T'HH:mm:sszzz",    // Contoh: "2025-05-30T07:30:55+07:00"
            "yyyy-MM-dd'T'HH:mm:ssK"       // Contoh: "2025-05-30T07:30:55Z" (UTC)
        };

        // Coba parse sebagai DateTimeOffset jika ada offset
        if (DateTimeOffset.TryParseExact(
                dateTimeString,
                formatsWithOffset,
                null, // CultureInfo.InvariantCulture
                System.Globalization.DateTimeStyles.None,
                out var dateTimeOffset))
        {
            // Convert ke UTC untuk konsistensi
            return dateTimeOffset.UtcDateTime.AddHours(7); // Tambahkan +7 jam
        }

        // Fallback ke DateTime jika tidak ada offset
        if (DateTime.TryParse(dateTimeString, out var dateTime))
        {
            return dateTime; // Simpan apa adanya
        }

        throw new ArgumentException("Invalid datetime format.");
    }
    
    public static DateTime ParseAndAdjustToUtcPlus7(string dateTimeString)
    {
        if (DateTimeOffset.TryParse(dateTimeString, out var dateTimeOffset))
        {
            // Convert ke UTC
            var utcDateTime = dateTimeOffset.UtcDateTime;

            // Adjust ke UTC+7
            return utcDateTime.AddHours(7);
        }

        if (DateTime.TryParse(dateTimeString, out var dateTime))
        {
            // Jika tidak ada offset, asumsikan sudah dalam UTC+7
            return dateTime;
        }

        throw new ArgumentException("Invalid datetime format.");
    }
    
    public static DateTime ParseToJakartaLocal(string input)
    {
        // Coba parse ke DateTimeOffset (akan otomatis handle +07:00, Z, dsb)
        if (DateTimeOffset.TryParse(input, null, DateTimeStyles.RoundtripKind, out var dto))
        {
            // Konversi ke Asia/Jakarta
            var jakartaTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // Windows
            // Untuk Linux gunakan: "Asia/Jakarta"
            if (OperatingSystem.IsLinux())
                jakartaTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
            // Jika input TANPA offset, DateTimeOffset akan Kind=Unspecified, Offset=00:00
            // Asumsikan waktu lokal Jakarta
            if (dto.Offset == TimeSpan.Zero && !input.EndsWith("Z") && !input.Contains("+"))
            {
                // Anggap waktu lokal Jakarta
                return DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Unspecified);
            }
            else
            {
                // Konversi ke waktu Jakarta
                return TimeZoneInfo.ConvertTime(dto, jakartaTz).DateTime;
            }
        }
        else
        {
            throw new FormatException("Format waktu tidak dikenali: " + input);
        }
    }
    
    
    
    private async Task<GpsLastPositionH> CreateNewGpsLastPosition(GpsVendor vendor, IList<GpsLastPositionD> lpsLastPositionDs, CancellationToken cancellationToken)
    {
        

        var dateTimeNow = DateTime.UtcNow;
        var createdBy = "System"; // Atau ambil dari konteks pengguna yang sedang aktif
        
        var h = GpsLastPositionH.Create(
            Guid.NewGuid(), vendor.Id
        );

        h.CreatedAt = dateTimeNow;  
        h.CreatedBy = createdBy;


        var gpsDelivery = new List<GpsDelivery>();
        
        // Kumpulkan semua PlatNo dari lpsLastPositionDs
        var platNos = lpsLastPositionDs
            .Where(l => !string.IsNullOrEmpty(l.PlatNo))
            .Select(l => l.PlatNo)
            .Distinct()
            .ToList();


        // Ambil semua data delivery progress sekaligus menggunakan PlatNo
        using var scopeDeliveryProgress = _scopeFactory.CreateScope();
        var repositoryDeliveryProgress = scopeDeliveryProgress.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
        var deliveryProgresses = await repositoryDeliveryProgress.GetCustomDeliveryProgressesAsync(platNos, cancellationToken);

        // Buat dictionary untuk mempermudah pencarian data berdasarkan PlatNo
        var deliveryProgressDict = deliveryProgresses.ToDictionary(dp => dp.PlatNo);
        
        
        foreach (var lpsLastPositionD in lpsLastPositionDs)
        {
            lpsLastPositionD.GpsLastPositionHId = h.Id;
            lpsLastPositionD.CreatedAt = dateTimeNow;
            lpsLastPositionD.CreatedBy = createdBy;
            
            // Cek apakah PlatNo ada dalam hasil query
            if (deliveryProgressDict.TryGetValue(lpsLastPositionD.PlatNo ?? string.Empty, out var deliveryProgress))
            {
                var item = new GpsDelivery
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendor.Id,
                    GpsVendorName = vendor.VendorName,
                    GpsDeliveryHId = h.Id,
                    //LpcdId = deliveryProgress.Lpcd,
                    Lpcd = deliveryProgress.Lpcd,
                    DeliveryNo = deliveryProgress.DeliveryNo,
                    NoKtp = deliveryProgress.NoKtp,
                    PlatNo = deliveryProgress.PlatNo,
                    DeviceId = lpsLastPositionD.DeviceId,
                    Datetime = lpsLastPositionD.Datetime,
                    X = lpsLastPositionD.X,
                    Y = lpsLastPositionD.Y,
                    Speed = lpsLastPositionD.Speed,
                    Course = lpsLastPositionD.Course,
                    StreetName = lpsLastPositionD.StreetName,
                    
                    CreatedAt = dateTimeNow,
                    CreatedBy = createdBy
                };
                gpsDelivery.Add(item);
            }
            /*
            using var scopeDeliveryProgress = _scopeFactory.CreateScope();
            var repositoryDeliveryProgress = scopeDeliveryProgress.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
            var deliveryProgress =  await repositoryDeliveryProgress.GetCustomDeliveryProgressAsync(lpsLastPositionD.PlatNo ?? string.Empty, cancellationToken);
            if (deliveryProgress != null) 
            {
                var item = new GpsDelivery
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendor.Id,
                    GpsVendorName = vendor.VendorName,
                    GpsDeliveryHId = h.Id,
                    //LpcdId = deliveryProgress.Lpcd,
                    Lpcd = deliveryProgress.Lpcd,
                    DeliveryNo = deliveryProgress.DeliveryNo,
                    NoKtp = deliveryProgress.NoKtp,
                    PlatNo = deliveryProgress.PlatNo,
                    DeviceId = lpsLastPositionD.DeviceId,
                    Datetime = lpsLastPositionD.Datetime,
                    X = lpsLastPositionD.X,
                    Y = lpsLastPositionD.Y,
                    Speed = lpsLastPositionD.Speed,
                    Course = lpsLastPositionD.Course,
                    StreetName = lpsLastPositionD.StreetName,
                    
                    CreatedAt = dateTimeNow,
                    CreatedBy = createdBy
                };
                gpsDelivery.Add(item);
                
            }*/
            
            h.AddGpsLastPositionD(lpsLastPositionD);
        }
        // Di dalam scope ini, Anda dapat mendapatkan instance layanan
        using var scope = _scopeFactory.CreateScope();
        
        var repository = scope.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
        
        await repository.InsertGpsLastPositionH(h, cancellationToken);
        await repository.InsertGpsDelivery(gpsDelivery, cancellationToken);

        return h;
    }
    
    private async Task<GpsLastPositionH> CreateNewGpsDelivery(GpsVendor vendor, IList<GpsLastPositionD> lpsLastPositionDs)
        {
            
            var dateTimeNow = DateTime.UtcNow;
            var createdBy = "System"; // Atau ambil dari konteks pengguna yang sedang aktif
    
            var h = GpsLastPositionH.Create(
                Guid.NewGuid(), vendor.Id
            );
            h.CreatedAt = dateTimeNow;
            h.CreatedBy = createdBy;
            
            foreach (var lpsLastPositionD in lpsLastPositionDs)
            {
                lpsLastPositionD.GpsLastPositionHId = h.Id;
                lpsLastPositionD.CreatedAt = dateTimeNow;
                lpsLastPositionD.CreatedBy = createdBy;
                h.AddGpsLastPositionD(lpsLastPositionD);
            }
    
            using var scope = _scopeFactory.CreateScope();
            // Di dalam scope ini, Anda dapat mendapatkan instance layanan
            var repository = scope.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
            await repository.InsertGpsLastPositionH(h);
    
            return h;
        }
    
    private static GpsLastPositionHDto? CreateGpsMessage(GpsVendor? vendor, List<GpsLastPositionD>? details)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor), "Vendor cannot be null.");
        }
        
        if (details == null)
        {
            throw new ArgumentNullException(nameof(details), "GpsLastPositionD cannot be null.");
        }

        var gpsMessage = new GpsLastPositionHDto
        {
            Id = details.First().GpsLastPositionHId,
            GpsVendorId = vendor.Id,
            VendorName = vendor.VendorName,
            CreatedAt = vendor.CreatedAt,
            LastModified = vendor.LastModified,
            
            Data = details.Select(detail => new GpsLastPositionDDto
            {
                Id = detail.Id ,
                GpsLastPositionHId = detail.GpsLastPositionHId,
                Lpcd = detail.Lpcd,
                PlatNo = detail.PlatNo,
                DeviceId = detail.DeviceId,
                Datetime = detail.Datetime,
                X = detail.X,
                Y = detail.Y,
                Speed = detail.Speed,
                Course = detail.Course,
                StreetName = detail.StreetName
            }).ToList()
        };

        return gpsMessage;
    }
    
    // Method to get the auth token from GpsVendorAuth BaseUrl
    private async Task<string> GetAuthTokenAsync(GpsVendorAuth? auth)
    {
        if (auth == null)
        {
            throw new ArgumentNullException(nameof(auth), "GpsVendorAuth cannot be null.");
        }
        
        var client = _httpClientFactory.CreateClient();
        try
        {
            // Prepare the request to get the token from the GpsVendorAuth BaseUrl
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod(auth.Method), // Assuming POST request, can be adjusted based on your requirements
                RequestUri = new Uri(auth.BaseUrl),
            };

            // Check Content-Type and handle Bodies dynamically
            if (auth.ContentType == "application/json")
            {
                // If Content-Type is JSON, serialize the body to JSON
                var jsonBody = auth.Bodies?.ToString() ?? "{}";  // Default empty JSON if no body
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }
            else if (auth.ContentType == "application/x-www-form-urlencoded")
            {
                // If Content-Type is form URL encoded, convert body to FormUrlEncodedContent
                var formData = new List<KeyValuePair<string, string>>();

                // Convert JsonObject to key-value pairs for form data
                if (auth.Bodies != null)
                {
                    foreach (var pair in auth.Bodies.AsObject())
                    {
                        formData.Add(new KeyValuePair<string, string>(pair.Key, pair.Value?.ToString() ?? ""));
                    }
                }

                request.Content = new FormUrlEncodedContent(formData);
            }
            else
            {
                // Default content type if not specified
                request.Content = new StringContent(auth.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json");
            }
            
            
            if (auth.Authtype == "Basic")
            {
                if (string.IsNullOrEmpty(auth.Username)) ArgumentException.ThrowIfNullOrEmpty(nameof(auth.Username));
                    
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                 client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(auth.Authtype, authValue);
            }
            

            // Send request to get token
            var tokenResponse = await client.SendAsync(request);
            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenResult = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully retrieved token for Vendor {VendorName}: {Token}", auth.GpsVendor?.VendorName, tokenResult);

                // Parse the response to extract token dynamically based on TokenPath
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(tokenResult); // Deserialize to JsonElement

                // Use the TokenPath to extract the token dynamically
                var tokenPath = auth.TokenPath?.Split('.') ?? Array.Empty<string>();
                var token = ExtractTokenPath(jsonResponse, tokenPath); // Use ExtractToken with JsonElement

                ////var dtToken = GetDataItems(tokenResult, "message.data");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Token not found in the response for Vendor {VendorName}", auth.GpsVendor?.VendorName);
                    return string.Empty; // Return empty string if no token found
                }

                return token; // Return the token string extracted from the response
            }
            else
            {
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();
                
                // Log the status code and the response body to help diagnose the error
                _logger.LogError("Failed to get token for Vendor {VendorName}: {StatusCode} {ReasonPhrase} - Response: {ResponseBody}", 
                    auth.GpsVendor?.VendorName, tokenResponse.StatusCode, tokenResponse.ReasonPhrase, responseContent);
                
                using (var scopeGpsApiLog = _scopeFactory.CreateScope()) 
                {
                    var iGpsApiLogRepository = scopeGpsApiLog.ServiceProvider.GetRequiredService<IGpsApiLogRepository>();
                    await iGpsApiLogRepository.InsertGpsApiLog(
                        new GpsApiLog(
                            Guid.NewGuid(), 
                            tokenResponse.RequestMessage?.RequestUri?.ToString(),
                            "0",
                            tokenResponse.ReasonPhrase,
                            responseContent,
                            auth.GpsVendor?.VendorName,
                            "",
                            DateTime.UtcNow,
                            "Auth"
                        ));
                }

                
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving token for Vendor {VendorName}", auth.GpsVendor?.VendorName);
            return string.Empty;
        }
    }
    
    // Helper method to extract token dynamically using the TokenPath
    private string ExtractTokenPath(JsonElement jsonResponse, string[] tokenPath)
    {
        JsonElement currentElement = jsonResponse;

        foreach (var path in tokenPath)
        {
            // Try to get property based on the path
            if (currentElement.TryGetProperty(path, out JsonElement nextElement))
            {
                currentElement = nextElement;
            }
            else
            {
                return string.Empty; // Return empty if path does not exist
            }
        }

        return currentElement.GetString() ?? string.Empty; // Return the token or empty if not found
    }
    
}

public class TestMessage
{
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
    
public class ListGpsLastPosition
{
    public ListGpsLastPosition()
    {
        
    }
    public List<GpsLastPositionD> GpsLastPositions { get; set; } = [];
}

public class GpsLastPositionListMessage
{
    public IList<GpsLastPositionD> Positions { get; set; } = new List<GpsLastPositionD>();
}
