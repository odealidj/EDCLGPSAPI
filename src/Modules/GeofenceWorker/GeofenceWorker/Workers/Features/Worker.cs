using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GeofenceWorker.Workers.Features;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly IBus _bus;
    ////private readonly IPublishEndpoint _publishEndpoint;
    public Worker( IServiceScopeFactory scopeFactory, 
        IBus bus,
        ////IPublishEndpoint publishEndpoint,
        HttpClient httpClient, 
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
        _bus = bus; 
        ////_publishEndpoint = publishEndpoint;
        _logger = logger;
        
        
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope()) // Create a scope for DbContext
                {
                    var context = scope.ServiceProvider.GetRequiredService<GeofenceWorkerDbContext>();
                    
                    // Get all vendors that need to call endpoints
                    var vendors = await context.GpsVendors
                         .Where(x => x.Id == Guid.Parse("cd742c69-2a56-46e4-afef-08fb0e0ce309"))
                        .Include(v => v.Auth)
                        .ToListAsync(stoppingToken);

                    foreach (var vendor in vendors)
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
                }

                // Wait for 1 minute (60,000 milliseconds) before next cycle
                await Task.Delay(180000, stoppingToken); // 1 minute delay
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task ProcessCombinedEndpoints(GpsVendor vendor, List<GpsVendorEndpoint> endpoints, IServiceScope scope,
        GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(vendor.ProcessingStrategyPathKey);
        
        var responses = new List<string>();
        //List<Dictionary<string, object>>? responses = null;
        foreach (var endpoint in endpoints)
        {
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod(endpoint.Method),
                RequestUri = new Uri(endpoint.BaseUrl),
                Content = new StringContent(endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json")
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
            if (endpoint.Params != null)
            {
                var parameters = endpoint.Params;
                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

                foreach (var param in parameters.AsObject())
                {
                    query[param.Key] = param.Value?.ToString();
                }

                request.RequestUri = new UriBuilder(request.RequestUri)
                {
                    Query = query.ToString()
                }.Uri;
            }
        
            // Set Authorization Header if required
            /*
            if (endpoint.GpsVendor is { RequiredAuth: true, Auth: not null })
            {
                if (endpoint.GpsVendor.Auth.Authtype == "Basic")
                {
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{endpoint.GpsVendor.Auth.Username}:{endpoint.GpsVendor.Auth.Password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                }
                else
                {
                    var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                }
            }
            */

            if (endpoint.GpsVendor is { RequiredAuth: true })
            {
                if (endpoint.GpsVendor.AuthType == "Basic")
                {
                    if (string.IsNullOrEmpty(endpoint.GpsVendor.Username)) ArgumentException.ThrowIfNullOrEmpty(nameof(endpoint.GpsVendor.Username));
                
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{endpoint.GpsVendor.Username}:{endpoint.GpsVendor.Password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authValue);
                }
                else if (endpoint.GpsVendor.AuthType == "Bearer")
                {
                    var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authToken);
                }
            }

            //JsonDocument? responseData = null;
            try
            {

                var response = await _httpClient.SendAsync(request, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    /*
                    responseData = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(stoppingToken));
                    if (responseData.RootElement.TryGetProperty("data", out var vehiclePositionsDataElement) &&
                        vehiclePositionsDataElement.ValueKind == JsonValueKind.Array)
                    {
                        responses = vehiclePositionsDataElement.Deserialize<List<Dictionary<string, object>>>();
                    }
                    */
                    /*
                    //vehiclePositionsDocument =
                    //    await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(stoppingToken), cancellationToken: stoppingToken);

                    responseData1 = await response.Content.ReadAsStringAsync(stoppingToken);

                    var data =  GetDataItems(responseData1, "data");
                    
                    vehiclePositions = new List<Dictionary<string, object>>() { JsonSerializer.Deserialize<Dictionary<string, 
                    */
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
                    return; // Hentikan pemrosesan jika salah satu endpoint gagal (opsional, bisa disesuaikan)

                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calling endpoint for Vendor {VendorName} - {BaseUrl}: {ErrorMessage}",
                    vendor.VendorName, endpoint.BaseUrl, ex.Message);
                return;
            }
            finally
            {
                
            }
            
        }
        
        
        if (responses.Count != 0)
        {
            var lastPositionH = new GpsLastPositionH
            {
                Id = Guid.NewGuid(),
                GpsVendorId = vendor.Id,
            };
            
            var combinedDataList = CombineJsonToString(responses, vendor.ProcessingStrategyPathKey??"", vendor.ProcessingStrategyPathData??"data");
            
            var lastPositionDs = await ProcessMappingResponse(vendor.Id ,combinedDataList, vendor.ProcessingStrategyPathData??"data", context);

            var geofenceMaster = await CreateNewGpsLastPosition(vendor, lastPositionDs);  
            
            _logger.LogInformation($"Vendor: {vendor.VendorName }, Data yang Digabungkan: {JsonSerializer.Serialize(combinedDataList, new JsonSerializerOptions { WriteIndented = true })}");
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
    
    /*
    private JsonObject? MergeResponses(GpsVendor vendor, List<string> responses)
    {
        if (vendor.ProcessingStrategy?.ToLowerInvariant() == "combined" && !string.IsNullOrEmpty(vendor.ProcessingStrategyPathKey))
        {
            _logger.LogInformation("Merging responses for Vendor {VendorName} using column: {ProcessingStrategyColumn}", vendor.VendorName, vendor.ProcessingStrategyPathKey);

            if (responses.Count == 2)
            {
                try
                {
                    var jsonObjects = new List<JsonObject?>();
                    foreach (var response in responses)
                    {
                        var node = JsonNode.Parse(response);
                        if (node is JsonObject jsonObject)
                        {
                            jsonObjects.Add(jsonObject);
                        }
                        else
                        {
                            _logger.LogError("Response for Vendor {VendorName} is not a valid JSON object: {Response}", vendor.VendorName, response);
                            return null;
                        }
                    }

                    if (jsonObjects.All(jo => jo != null))
                    {
                        var firstData = jsonObjects[0]?["data"]?.AsArray();
                        var secondData = jsonObjects[1]?["data"]?.AsArray();
                        var keyColumn = vendor.ProcessingStrategyPathKey;
                        var mergedData = new JsonArray();
                        var seenKeys = new Dictionary<string, JsonObject>();

                        // Process data from the first response
                        if (firstData != null)
                        {
                            foreach (var itemNode in firstData)
                            {
                                if (itemNode is JsonObject item)
                                {
                                    var keyValue = item?[keyColumn]?.ToString();
                                    if (!string.IsNullOrEmpty(keyValue) && !seenKeys.ContainsKey(keyValue))
                                    {
                                        seenKeys.Add(keyValue, item);
                                    }
                                }
                            }
                        }

                        // Process data from the second response and merge
                        if (secondData != null)
                        {
                            foreach (var itemNode in secondData)
                            {
                                if (itemNode is JsonObject item)
                                {
                                    var keyValue = item?[keyColumn]?.ToString();
                                    if (!string.IsNullOrEmpty(keyValue))
                                    {
                                        if (seenKeys.ContainsKey(keyValue))
                                        {
                                            // Merge properties from the current item into the existing merged object
                                            foreach (var property in item.AsObject())
                                            {
                                                if (!seenKeys[keyValue].ContainsKey(property.Key))
                                                {
                                                    seenKeys[keyValue].Add(property.Key, property.Value);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            seenKeys.Add(keyValue, item);
                                        }
                                    }
                                }
                            }
                        }

                        // Convert the merged objects back to a JsonArray
                        foreach (var mergedObject in seenKeys.Values)
                        {
                            mergedData.Add(mergedObject);
                        }

                        return new JsonObject { ["data"] = mergedData };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError("Error parsing JSON response for Vendor {VendorName}: {ErrorMessage}", vendor.VendorName, ex.Message);
                }
            }
            else
            {
                _logger.LogError("Expected 2 responses for combined processing of Vendor {VendorName}, but got {responses.Count}.", vendor.VendorName);
            }
        }
        else if (vendor.ProcessingStrategy?.ToLowerInvariant() == "combined" && string.IsNullOrEmpty(vendor.ProcessingStrategyPathKey))
        {
            _logger.LogError("ProcessingStrategyColumn is not set for Vendor {VendorName} with ProcessingStrategy 'Combined'.", vendor.VendorName);
        }
        return null;
    }
    */
    
    /*
    private async Task ProcessMergedData(GpsVendor vendor, JsonObject mergedData, IServiceScope scope, GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing merged data for Vendor {VendorName}: {Data}", vendor.VendorName, mergedData.ToJsonString());
        // Implement your logic to process the merged data here.
        // You might want to save this data to the database or perform other actions.
    }
    */

    private async Task ProcessIndividualEndPoint(GpsVendorEndpoint endpoint, IServiceScope scope, GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(endpoint.Method),
            RequestUri = new Uri(endpoint.BaseUrl),
            Content = new StringContent(endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json")
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
        /*
        if (endpoint.Params != null)
        {
            var parameters = endpoint.Params;
            foreach (var param in parameters.AsObject())
            {
                request.RequestUri = new UriBuilder(request.RequestUri)
                {
                    Query = $"{param.Key}={param.Value?.ToString()}"
                }.Uri;
            }
        }
        */
        
        if (endpoint.Params != null)
        {
            var parameters = endpoint.Params;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

            foreach (var param in parameters.AsObject())
            {
                query[param.Key] = param.Value?.ToString();
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
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authValue);
            }
            else if (endpoint.GpsVendor.AuthType == "Bearer")
            {
                var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authToken);
            }
        }
        
        var response = await _httpClient.SendAsync(request, stoppingToken);
        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync(stoppingToken);
            
            
            var lastPositionDs = await ProcessMappingResponse(
                endpoint.GpsVendor.Id,
                responseData, 
                endpoint.GpsVendor.ProcessingStrategyPathData??"data",
                context);

            if (lastPositionDs.Count > 0)
            {
                var geofenceMaster = await CreateNewGpsLastPosition(endpoint.GpsVendor, lastPositionDs);
            }

            /*
            //await _bus.Publish(responseMapping, stoppingToken);
            
            var message = new TestMessage
            {
                Text = "Hello, MassTransit with RabbitMQ!"
            };

            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();  // Mendapatkan IPublishEndpoint dari scope

            var listGpsLastPosition = new ListGpsLastPosition
            {
                GpsLastPositions = responseMapping.ToList()
            };
            
            // Mengirim pesan ke RabbitMQ
            await _bus.Publish(listGpsLastPosition, stoppingToken);
            
            */
            
            _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}: {ResponseData}", endpoint.GpsVendor.VendorName, responseData);
            
            
            // Pass the context to MapResponseToDatabase method
            ////var mappedData = await MapResponseToDatabase(responseData, vendor.Id, context);

            // Process the mapped data
            ////_logger.LogInformation("Successfully mapped data for Vendor {VendorName}: {MappedData}", vendor.VendorName, mappedData);                    
        }
        else
        {
            _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", endpoint.GpsVendor?.VendorName, response.StatusCode, response.ReasonPhrase);
        }
    }


    /*
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope()) // Create a scope for DbContext
            {
                var context = scope.ServiceProvider.GetRequiredService<GeofenceWorkerDbContext>();
                
                // Get all vendors that need to call endpoints
                var vendors = await context.GpsVendors
                    //.Include(v => v.Endpoints)
                    .Include(v => v.Auth)
                    //.Where(v => v.Endpoint != null  && v.Id == Guid.Parse("64da8379-62c7-4ff4-8c0c-b2a064d6657d"))
                    //.Where(v => v.Endpoint != null && new[] { Guid.Parse("a52d4709-3aa4-45e1-9ca6-537e93bc7a9d"), Guid.Parse("64da8379-62c7-4ff4-8c0c-b2a064d6657d") }.Contains(v.Id))
                    //.Where(v => true)
                    .ToListAsync(stoppingToken);

                foreach (var vendor in vendors)
                {
                    var request = new HttpRequestMessage
                    {
                        Method = new HttpMethod(vendor.Endpoints.Method),
                        RequestUri = new Uri(vendor.Endpoint.BaseUrl),
                        Content = new StringContent(vendor.Endpoint.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json")
                    };
                    
                    // Add Headers from JsonObject if any
                    if (vendor.Endpoint.Headers != null)
                    {
                        foreach (var header in vendor.Endpoint.Headers.AsObject())
                        {
                            // Add each header from the JsonObject to the request headers
                            request.Headers.Add(header.Key, header.Value?.ToString());
                        }
                    }
                    
                    
                    // Attach parameters to the URL if any
                    if (vendor.Endpoints.Params != null)
                    {
                        var parameters = vendor.Endpoint.Params;
                        foreach (var param in parameters.AsObject())
                        {
                            request.RequestUri = new UriBuilder(request.RequestUri)
                            {
                                Query = $"{param.Key}={param.Value?.ToString()}"
                            }.Uri;
                        }
                    }
                    
                    // Set Authorization Header if required
                    if (vendor.RequiredAuth && vendor.Auth != null)
                    {
                        if (vendor.Auth.Authtype == "Basic")
                        {
                            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{vendor.Auth.Username}:{vendor.Auth.Password}"));
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                        }
                        else
                        {
                            var authToken = await GetAuthTokenAsync(vendor.Auth);
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                        }
                    }
                    
                    var response = await _httpClient.SendAsync(request, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        
                        
                        var responseMapping = await ProcessVendorResponse(vendor,responseData, context);

                        //await _bus.Publish(responseMapping, stoppingToken);
                        
                        var message = new TestMessage
                        {
                            Text = "Hello, MassTransit with RabbitMQ!"
                        };

                        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();  // Mendapatkan IPublishEndpoint dari scope

                        var listGpsLastPosition = new ListGpsLastPosition
                        {
                            GpsLastPositions = responseMapping
                        };
                        
                        // Mengirim pesan ke RabbitMQ
                        await _bus.Publish(listGpsLastPosition, stoppingToken);
                        
                        _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}: {ResponseData}", vendor.VendorName, responseData);
                        
                        
                        // Pass the context to MapResponseToDatabase method
                        ////var mappedData = await MapResponseToDatabase(responseData, vendor.Id, context);

                        // Process the mapped data
                        ////_logger.LogInformation("Successfully mapped data for Vendor {VendorName}: {MappedData}", vendor.VendorName, mappedData);                    
                    }
                    else
                    {
                        _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", vendor.VendorName, response.StatusCode, response.ReasonPhrase);
                    }
                }

            }

            // Wait for 1 minute (60,000 milliseconds) before next cycle
            await Task.Delay(60000, stoppingToken); // 1 minute delay
        }
    }
    */
    
    /*
    private async Task ProcessVendorResponse1(GpsVendor gpsVendor, string jsonResponse, GeofenceWorkerDbContext _context)
    {
        // 1. Ambil semua mapping untuk vendor
        var mappings = await _context.Mappings
            .Where(v => v.GpsVendorId == gpsVendor.Id)
            .ToListAsync();

        // 2. Parse JSON sebagai JToken (bisa object atau array)
        var token = JToken.Parse(jsonResponse);
        List<JToken> dataItems = new List<JToken>();

        // 3. Deteksi tipe root JSON
        if (token is JArray rootArray)
        {
            // Jika root adalah array (contoh: [ {...}, {...} ])
            dataItems = rootArray.Children().ToList();
        }
        else if (token is JObject rootObject)
        {
            // Jika root adalah object (contoh: { "data": [...] })
            string dataPath = mappings.First().DataPath ?? string.Empty;
            dataItems = rootObject.SelectToken(dataPath)?.Children().ToList() ?? new List<JToken>();
        }

        foreach (var dataItem in dataItems)
        {
            
            
            var gpsLastPosition = new GpsLastPosition
            {
                Id = Guid.NewGuid(),
                GpsVendorId = gpsVendor.Id
            };

            foreach (var mapping in mappings)
            {
                try
                {
                    // 4. Ekstrak nilai dari JSON
                    var valueToken = dataItem.SelectToken(mapping.ResponseField);
                    if (valueToken == null) continue;

                    // 5. Set properti di VehicleData menggunakan refleksi
                    PropertyInfo? property = typeof(GpsLastPosition).GetProperty(mapping.MappedField);
                    if (property == null) continue;

                    //object? value = valueToken.ToObject(property.PropertyType);
                    
                    object value;
                    Type propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (propType == typeof(DateTime))
                    {
                        if (valueToken.Type == JTokenType.Null)
                        {
                            value = null;
                        }
                        else
                        {
                            var dateTimeValue = valueToken.ToObject<DateTime>();
                            value = dateTimeValue.Kind == DateTimeKind.Unspecified 
                                ? DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc)
                                : dateTimeValue.ToUniversalTime();
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


            
            await _context.GpsLastPositions.AddAsync(CreateNewGpsLastPosition(gpsLastPosition));
        }

        await _context.SaveChangesAsync();
    }
    */
    
    /*
    private List<JToken> GetDataItems(string jsonResponse, string dataPath)
    {
        var token = JToken.Parse(jsonResponse);
        List<JToken> dataItems = new List<JToken>();

        if (token is JArray rootArray)
        {
            dataItems = rootArray.Children().ToList();
        }
        else if (token is JObject rootObject)
        {
            dataItems = rootObject.SelectToken(dataPath)?.Children().ToList() ?? new List<JToken>();
        }

        return dataItems;
    }
    */
    private List<JToken> GetDataItems(string jsonResponse, string dataPath)
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
    }
    
    /*
    public static List<Dictionary<string, object>> CombineJson(List<string> jsonResponses, string key)
    {
        if ( jsonResponses.Count == 0)
        {
            return [];
            //return new Dictionary<string, object>();
        }

        var parsedResponses = new List<List<Dictionary<string, object>>>();
        foreach (var jsonResponse in jsonResponses)
        {
            try
            {
                var document = JsonDocument.Parse(jsonResponse);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var items = document.RootElement.Deserialize<List<Dictionary<string, object>>>();
                    if (items != null)
                    {
                        parsedResponses.Add(items);
                    }
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var item = document.RootElement.Deserialize<Dictionary<string, object>>();
                    if (item != null)
                    {
                        parsedResponses.Add(new List<Dictionary<string, object>> { item });
                    }
                }
            }
            catch (JsonException ex)
            {
                // Handle parsing errors, log or throw as needed
                System.Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        if (parsedResponses.Count < 2 )
        {
            throw new InvalidOperationException("Parsed responses must contain at least 2 items.");
        }

        var firstResponse = parsedResponses[0];
        var combinedList = new List<Dictionary<string, object>>();

        foreach (var firstItem in firstResponse)
        {
            if (firstItem.TryGetValue(key, out var keyValue) && keyValue != null)
            {
                var combinedItem = new Dictionary<string, object>(firstItem);
                foreach (var otherResponse in parsedResponses.Skip(1))
                {
                    var matchingItem = otherResponse.FirstOrDefault(item =>
                        item.ContainsKey(key) && item[key]?.Equals(keyValue) == true);

                    if (matchingItem != null)
                    {
                        foreach (var prop in matchingItem)
                        {
                            if (!combinedItem.ContainsKey(prop.Key))
                            {
                                combinedItem[prop.Key] = prop.Value;
                            }
                        }
                    }
                }
                combinedList.Add(combinedItem);
            }
            else
            {
                // If the key doesn't exist in the first item, just add it without combining
                combinedList.Add(firstItem);
            }
        }

        // Add items from subsequent responses that don't have a match in the first response
        foreach (var otherResponse in parsedResponses.Skip(1))
        {
            foreach (var otherItem in otherResponse)
            {
                if (otherItem.TryGetValue(key, out var otherKeyValue) && otherKeyValue != null)
                {
                    if (!combinedList.Any(combinedItem =>
                            combinedItem.ContainsKey(key) && combinedItem[key]?.Equals(otherKeyValue) == true))
                    {
                        combinedList.Add(otherItem);
                    }
                }
                else
                {
                    // If the key doesn't exist in the other item, add it if not already present
                    if (!combinedList.Contains(otherItem))
                    {
                        combinedList.Add(otherItem);
                    }
                }
            }
        }

        return combinedList;
    }
    */
    public static Dictionary<string, object> CombineJson3(List<string> jsonResponses, string key)
    {
        if (jsonResponses == null || !jsonResponses.Any())
        {
            return new Dictionary<string, object> { ["page"] = 1, ["limit"] = 100, ["total"] = 0, ["data"] = new List<Dictionary<string, object>>() };
        }

        var parsedResponses = new List<Dictionary<string, object>>();
        int totalCombined = 0;
        int page = 1;
        int limit = 100;

        foreach (var jsonResponse in jsonResponses)
        {
            try
            {
                var document = JsonDocument.Parse(jsonResponse);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    parsedResponses.Add(document.RootElement.Deserialize<Dictionary<string, object>>()!);
                    if (document.RootElement.TryGetProperty("page", out var pageElement) && pageElement.ValueKind == JsonValueKind.Number)
                    {
                        page = pageElement.GetInt32();
                    }
                    if (document.RootElement.TryGetProperty("limit", out var limitElement) && limitElement.ValueKind == JsonValueKind.Number)
                    {
                        limit = limitElement.GetInt32();
                    }
                }
            }
            catch (JsonException ex)
            {
                System.Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        var combinedDataList = new List<Dictionary<string, object>>();
        var dataLists = parsedResponses.Where(r => r.ContainsKey("data") && r["data"] is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                                       .Select(r => (r["data"] as JsonElement?)?.Deserialize<List<Dictionary<string, object>>>() ?? new List<Dictionary<string, object>>())
                                       .ToList();

        if (!dataLists.Any())
        {
            return new Dictionary<string, object> { ["page"] = page, ["limit"] = limit, ["total"] = 0, ["data"] = new List<Dictionary<string, object>>() };
        }

        var allItems = dataLists.SelectMany(list => list).ToList();
        var groupedByVin = allItems.GroupBy(item => item.ContainsKey(key) ? item[key]?.ToString() : null).Where(g => g.Key != null);

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

        totalCombined = combinedDataList.Count;

        return new Dictionary<string, object>
        {
            ["page"] = page,
            ["limit"] = limit,
            ["total"] = totalCombined,
            ["data"] = combinedDataList
        };
    }
    
    public static List<Dictionary<string, object>> CombineJson(List<string> jsonResponses, string key, string dataPath = "data")
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
                System.Console.WriteLine($"Error parsing JSON: {ex.Message}");
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
    
    public static Dictionary<string, object> CombineJson2(List<string> jsonResponses, string key)
    {
        if (jsonResponses == null || !jsonResponses.Any())
        {
            return new Dictionary<string, object> { ["data"] = new List<Dictionary<string, object>>() };
        }

        var parsedResponses = new List<Dictionary<string, object>>();
        foreach (var jsonResponse in jsonResponses)
        {
            try
            {
                var document = JsonDocument.Parse(jsonResponse);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    parsedResponses.Add(document.RootElement.Deserialize<Dictionary<string, object>>()!);
                }
            }
            catch (JsonException ex)
            {
                // Handle parsing errors, log or throw as needed
                System.Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        if (!parsedResponses.Any())
        {
            return new Dictionary<string, object> { ["data"] = new List<Dictionary<string, object>>() };
        }

        var combinedDataList = new List<Dictionary<string, object>>();
        var firstResponseData = parsedResponses.FirstOrDefault(r => r.ContainsKey("data"))?.GetValueOrDefault("data") as List<Dictionary<string, object>>;

        if (firstResponseData != null)
        {
            foreach (var firstItem in firstResponseData)
            {
                if (firstItem.TryGetValue(key, out var keyValue) && keyValue != null)
                {
                    var combinedItem = new Dictionary<string, object>(firstItem);
                    foreach (var otherResponse in parsedResponses.Skip(1).Where(r => r.ContainsKey("data")))
                    {
                        var otherResponseData = otherResponse.GetValueOrDefault("data") as List<Dictionary<string, object>>;
                        var matchingItem = otherResponseData?.FirstOrDefault(item =>
                            item.ContainsKey(key) && item[key]?.Equals(keyValue) == true);

                        if (matchingItem != null)
                        {
                            foreach (var prop in matchingItem)
                            {
                                if (!combinedItem.ContainsKey(prop.Key))
                                {
                                    combinedItem[prop.Key] = prop.Value;
                                }
                            }
                        }
                    }
                    combinedDataList.Add(combinedItem);
                }
                else
                {
                    combinedDataList.Add(firstItem);
                }
            }
        }
        else if (parsedResponses.Any()) // Handle case where 'data' is not present in the first response
        {
            foreach (var response in parsedResponses)
            {
                combinedDataList.AddRange(response
                    .Where(kvp => kvp.Value is Dictionary<string, object>)
                    .Select(kvp => (Dictionary<string, object>)kvp.Value)
                    .ToList());
            }
        }

        // Add items from subsequent 'data' arrays that don't have a match in the first 'data' array
        foreach (var otherResponse in parsedResponses.Skip(1).Where(r => r.ContainsKey("data")))
        {
            var otherResponseData = otherResponse.GetValueOrDefault("data") as List<Dictionary<string, object>>;
            if (otherResponseData != null)
            {
                foreach (var otherItem in otherResponseData)
                {
                    if (otherItem.TryGetValue(key, out var otherKeyValue) && otherKeyValue != null)
                    {
                        if (!combinedDataList.Any(combinedItem =>
                                combinedItem.ContainsKey(key) && combinedItem[key]?.Equals(otherKeyValue) == true))
                        {
                            combinedDataList.Add(otherItem);
                        }
                    }
                    else if (!combinedDataList.Contains(otherItem))
                    {
                        combinedDataList.Add(otherItem);
                    }
                }
            }
        }

        return new Dictionary<string, object>
        {
            ["data"] = combinedDataList
        };
    }
    public static string CombineJsonToString(List<string> jsonResponses, string key, string dataPath, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var combinedList = CombineJson(jsonResponses, key, dataPath);
        return JsonSerializer.Serialize(combinedList, jsonSerializerOptions);
    }
    
    private async Task<IList<GpsLastPositionD>> ProcessMappingResponse(Guid vendorId, string jsonResponse, string dataPath, GeofenceWorkerDbContext _context)
    {
        
        // 1. Ambil semua mapping untuk vendor
        var mappings = await _context.Mappings
            .Where(v => v.GpsVendorId == vendorId)
            .AsNoTracking()
            .ToListAsync();

        if (mappings.Count == 0) return [];
        var  dataItems = GetDataItems(jsonResponse, dataPath);
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
                            var dateTimeValue = valueToken.ToObject<DateTime>();
                            value = dateTimeValue.Kind == DateTimeKind.Unspecified 
                                ? DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc)
                                : dateTimeValue.ToUniversalTime();
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
    
    
    
     
    /*
    private async Task<List<GpsLastPosition>> ProcessVendorResponseOKOld(GpsVendor gpsVendor, string jsonResponse, GeofenceWorkerDbContext _context)
    {
        // 1. Ambil semua mapping untuk vendor
        var mappings = await _context.Mappings
            .Where(v => v.GpsVendorId == gpsVendor.Id)
            .ToListAsync();

        // 2. Parse JSON sebagai JToken (bisa object atau array)
        var token = JToken.Parse(jsonResponse);
        List<JToken> dataItems = new List<JToken>();

        // 3. Deteksi tipe root JSON
        if (token is JArray rootArray)
        {
            // Jika root adalah array (contoh: [ {...}, {...} ])
            dataItems = rootArray.Children().ToList();
        }
        else if (token is JObject rootObject)
        {
            // Jika root adalah object (contoh: { "data": [...] })
            string dataPath = mappings.First().DataPath ?? string.Empty;
            dataItems = rootObject.SelectToken(dataPath)?.Children().ToList() ?? new List<JToken>();
        }

        var gpsLastPositions = new List<GpsLastPosition>();

        foreach (var dataItem in dataItems)
        {
            var gpsLastPosition = new GpsLastPosition
            {
                Id = Guid.NewGuid(),
                GpsVendorId = gpsVendor.Id
            };

            foreach (var mapping in mappings)
            {
                try
                {
                    // 4. Ekstrak nilai dari JSON
                    var valueToken = dataItem.SelectToken(mapping.ResponseField);
                    if (valueToken == null) continue;

                    // 5. Set properti di VehicleData menggunakan refleksi
                    PropertyInfo? property = typeof(GpsLastPosition).GetProperty(mapping.MappedField);
                    if (property == null) continue;

                    object value;
                    Type propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (propType == typeof(DateTime))
                    {
                        if (valueToken.Type == JTokenType.Null)
                        {
                            value = null;
                        }
                        else
                        {
                            var dateTimeValue = valueToken.ToObject<DateTime>();
                            value = dateTimeValue.Kind == DateTimeKind.Unspecified 
                                ? DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc)
                                : dateTimeValue.ToUniversalTime();
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
    */
    
    private async Task<GpsLastPositionH> CreateNewGpsLastPosition(GpsVendor vendor, IList<GpsLastPositionD> lpsLastPositionDs)
    {

        var h = GpsLastPositionH.Create(
            Guid.NewGuid(), vendor.Id
        );
        
        foreach (var lpsLastPositionD in lpsLastPositionDs)
        {
            lpsLastPositionD.GpsLastPositionHId = h.Id;
            h.AddGpsLastPositionD(lpsLastPositionD);
        }

        using var scope = _scopeFactory.CreateScope();
        // Di dalam scope ini, Anda dapat mendapatkan instance layanan
        var repository = scope.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
        await repository.InsertGpsLastPositionH(h);

        return h;
    }
    
    
    // Metode untuk menentukan path data array (jika root bukan array)
    private string GetDataArrayPath(string vendorName)
    {
        // Contoh konfigurasi path:
        return vendorName switch
        {
            "GP" => "data",
            "PUNINAR" => "data",
            _ => "data" // Default
        };
    }

    // Validasi data wajib
    /*
    private bool IsValidVehicleData(VehicleData vehicle)
    {
        return !string.IsNullOrEmpty(vehicle.VehicleNumber) &&
               vehicle.Longitude != 0 &&
               vehicle.Latitude != 0;
    }
    */
    
    
    
    // Method to get the auth token from GpsVendorAuth BaseUrl
    private async Task<string> GetAuthTokenAsync(GpsVendorAuth auth)
    {
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
            
            
            // Prepare the request to get the token from the GpsVendorAuth BaseUrl
            ////var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
            
            /*
            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
            };
            */
           
            
            ////request.Content = new FormUrlEncodedContent(requestData);
            ////request.Headers.Add("accept", "application/json");
            ////request.Headers.Add("username", auth.Username);
            ////request.Headers.Add("password", auth.Password);
            ////request.Headers.Add("X-IBM-Client-Id", "1575255f-bc93-5x17-98fd-y01gf748581y");
            ////request.Headers.Add("Authorization", $"Basic {authHeader}");
            
            // Add Headers from JsonObject if any
            /*
            if (auth.Headers != null)
            {
                foreach (var header in auth.Headers.AsObject())
                {
                    // Add each header from the JsonObject to the request headers
                    request.Headers.Add(header.Key, header.Value?.ToString());
                }
            }
            */        
                    
            // Attach parameters to the URL if any
            /*
                if (endpoint.Params != null)
               {
                   var parameters = endpoint.Params;
                   var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

                   foreach (var param in parameters.AsObject())
                   {
                       query[param.Key] = param.Value?.ToString();
                   }

                   request.RequestUri = new UriBuilder(request.RequestUri)
                   {
                       Query = query.ToString()
                   }.Uri;
               }
            */

            if (auth.Authtype == "Basic")
            {
                if (string.IsNullOrEmpty(auth.Username)) ArgumentException.ThrowIfNullOrEmpty(nameof(auth.Username));
                    
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                 _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(auth.Authtype, authValue);
            }
            

            // Send request to get token
            var tokenResponse = await _httpClient.SendAsync(request);
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
                // Log the status code and the response body to help diagnose the error
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get token for Vendor {VendorName}: {StatusCode} {ReasonPhrase} - Response: {ResponseBody}", 
                    auth.GpsVendor?.VendorName, tokenResponse.StatusCode, tokenResponse.ReasonPhrase, responseContent);

                
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
    

    /*
    private async Task<string> MapResponseToDatabase(string responseData, Guid gpsVendorId, GeofenceWorkerDbContext context)
    {
        // Query the database to get mappings for the specific vendor
        var vendorMappings = await context.Mappings
            .Where(vm => vm.GpsVendorId == gpsVendorId)
            .ToListAsync();

        // Query the database to get the response format for the specific vendor
        var vendorResponseFormats = await context.ResponseFormats
            .Where(vrf => vrf.GpsVendorId == gpsVendorId)
            .ToListAsync();

        // Deserialize response data to JSON
        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseData);

        var mappedData = new Dictionary<string, object>();

        foreach (var mapping in vendorMappings)
        {
            // Get the response path for the current mapping
            var responseFormat = vendorResponseFormats.FirstOrDefault(x => x.MappedField == mapping.MappedField);

            if (responseFormat != null)
            {
                // Extract the value from JSON based on the response path
                var fieldValue = GetPropertyFromJsonPath(jsonResponse, responseFormat.ResponsePath);

                if (fieldValue != null)
                {
                    mappedData[mapping.MappedField] = fieldValue;
                }
            }
        }

        return JsonSerializer.Serialize(mappedData);
    }
    */
    
    // Helper method to extract the value from JSON based on the ResponsePath
    
    /*
    private object GetPropertyFromJsonPath(JsonElement jsonElement, string path)
    {
        var paths = path.Split('.');  // Split the path into parts

        // Check if the JSON element is an array (data is an array in the response)
        if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() > 0)
        {
            jsonElement = jsonElement[0];  // Take the first element from the array (data[0])
        }

        // Iterate through the path segments
        foreach (var segment in paths)
        {
            // If the current element is an object, try to get the property
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                if (jsonElement.TryGetProperty(segment, out var property))
                {
                    jsonElement = property;  // Move deeper into the object
                }
                else
                {
                    continue;  // Property not found, return null
                }
            }
            else
            {
                return null;  // If it's not an object, return null
            }
        }

        // Return the final value as string or null if not found
        return jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() : null;
    }
    */
    
 
}

public class TestMessage
{
    public string Text { get; set; } = string.Empty;
}
    
public class ListGpsLastPosition
{
    public List<GpsLastPositionD> GpsLastPositions { get; set; } = [];
}

/*
public class VendorMapper
{
    public static string GetMappedValue(string jsonResponse, string jsonPath)
    {
        var jsonObject = JObject.Parse(jsonResponse);
        return jsonObject.SelectToken(jsonPath)?.ToString();
    }
}
*/