using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Helper;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Workers.Dtos;
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
                // Contoh data yang ingin dipublish
                /*
                var dataToPublish = new { Message = $"Hello from Worker at {DateTime.UtcNow}" };
                string routingKey = "worker.message";
                string exchangeName = "my_topic_exchange";
                
                // Publikasikan pesan dengan routing key 'gps.position'
                await _rabbitMqService.PublishAsync(dataToPublish, "gps.position");
                */
                
                using (var scope = _scopeFactory.CreateScope()) // Create a scope for DbContext
                {
                    var context = scope.ServiceProvider.GetRequiredService<GeofenceWorkerDbContext>();
                    
                    // Get all vendors that need to call endpoints
                    var vendors = await context.GpsVendors
                         .Where(x => x.Id == Guid.Parse("4bb3ac8e-288b-44b7-83a7-da182967d7ec"))
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
                
                await Task.Delay(60000, stoppingToken); // 1 minute delay
                
                ////await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); 
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
        
        var client = _httpClientFactory.CreateClient();
        
        var responses = new List<string>();
        //List<Dictionary<string, object>>? responses = null;
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
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authValue);
                }
                else if (endpoint.GpsVendor.AuthType == "Bearer")
                {
                    var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(endpoint.GpsVendor.AuthType, authToken);
                }
            }

            //JsonDocument? responseData = null;
            try
            {

                var response = await client.SendAsync(request, stoppingToken);
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

    private async Task ProcessIndividualEndPoint(GpsVendorEndpoint endpoint, IServiceScope scope, GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        
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
            else if (endpoint.GpsVendor.AuthType == "Bearer")
            {
                var authToken = await GetAuthTokenAsync(endpoint.GpsVendor.Auth);
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
                context);

            var maxData = 0;
            
            if (lastPositionDs.Count > 0)
            {
                var geofenceMaster = await CreateNewGpsLastPosition(endpoint.GpsVendor, lastPositionDs);
                
                if (!string.IsNullOrEmpty(endpoint.MaxPath))
                {
                    var dataItens = await GetDataItems(responseData, endpoint.GpsVendor.ProcessingStrategyPathData??"data");
                    maxData = await FindMaxProperty.FindMaxPropertyValueWithExceptionAsync<int>(dataItens, endpoint.MaxPath);
                    await UpdateLastPositionId(endpoint, endpoint.MaxPath, maxData);
                }
                
                
                // Contoh data yang ingin dipublish
                
                ////var dataToPublish = new { Message = $"Hello from Worker at {DateTime.UtcNow}" };
                string routingKey = "worker.message";
                string exchangeName = "my_topic_exchange";

                var message =CreateGpsMessage(endpoint.GpsVendor, lastPositionDs.ToList());

                // Publikasikan pesan dengan routing key 'gps.position'
                
                
                var testMessage = new TestMessage
                {
                    Text = "Hello, MassTransit with RabbitMQ!",
                    Timestamp = DateTime.UtcNow
                };
                
                await _rabbitMqService.PublishAsync(testMessage, "gps.position");
            }


            //await _bus.Publish(responseMapping, stoppingToken);
            /*
            var message = new TestMessage
            {
                Text = "Hello, MassTransit with RabbitMQ!"
            };


            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();  // Mendapatkan IPublishEndpoint dari scope

            var listGpsLastPosition = new ListGpsLastPosition
            {
                GpsLastPositions = lastPositionDs.ToList()
            };
            
                */

            // Mengirim pesan ke RabbitMQ
            ////await _bus.Publish(listGpsLastPosition, stoppingToken);


            ////var payload.GpsVendorId = vendorId;
            ////payload.Lpcd = lpcd;

            // Tentukan routing key berdasarkan vendor dan LPCD (contoh)
            //string routingKey = $"gps.position.{endpoint.GpsVendor.VendorName.ToLower()}";

            // Publish pesan ke exchange default (atau yang dikonfigurasi untuk pesan ini)
            // dengan menyertakan routing key

            /*
            await  _publishEndpoint.Publish(listGpsLastPosition, x =>
            {
                // Secara eksplisit menentukan exchange type (opsional, bisa dikonfigurasi secara global)
                // x.SetExchangeKind(ExchangeType.Topic);

                // Menentukan routing key
                x.RoutingKey = routingKey;
            });
            */
            
            /*
            var message2 = new GpsLastPositionListMessage
            {
                
                Positions = new List<GpsLastPositionD>
                {
                    new GpsLastPositionD { Latitude = -6.175392, Longitude = 106.827153, Timestamp = DateTime.UtcNow },
                    new GpsLastPositionD { Latitude = -6.175400, Longitude = 106.827200, Timestamp = DateTime.UtcNow }
                }
                Positions = listGpsLastPosition.
            };
            */
            
            /*
            await _bus.Publish(listGpsLastPosition, x =>
            {
                // Set the routing key
                x.SetRoutingKey(routingKey);
            });
            */
            _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}: {ResponseData}", endpoint.GpsVendor.VendorName, responseData);
            
            
            // Pass the context to MapResponseToDatabase method
            ////var mappedData = await MapResponseToDatabase(responseData, vendor.Id, context);

            // Process the mapped data
            ////_logger.LogInformation("Successfully mapped data for Vendor {VendorName}: {MappedData}", vendor.VendorName, mappedData);                    
        }
        else
        {
            var responseData = await response.Content.ReadAsStringAsync(stoppingToken);
            _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", endpoint.GpsVendor?.VendorName, response.StatusCode, response.ReasonPhrase);
        }
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
            // Di dalam scope ini, Anda dapat mendapatkan instance layanan
            var repository = scope.ServiceProvider.GetRequiredService<IGpsLastPositionHRepository>();
            
            await repository.UpdateVarParamsPropertyRawSqlAsync(
                endpoint.Id, 
                "lastPositionId", 
                newLastPositionId,
                DateTime.UtcNow,
                "System");
            
            //await repository.UpdateVarParamsAsync(endpoint);

        }
        
        ////return false; 
        // varParams is null
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
    
    private async Task<GpsLastPositionH> CreateNewGpsLastPosition(GpsVendor vendor, IList<GpsLastPositionD> lpsLastPositionDs)
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
    
    public static GpsLastPostionDto? CreateGpsMessage(GpsVendor? vendor, List<GpsLastPositionD>? details)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor), "Vendor cannot be null.");
        }
        
        if (details == null)
        {
            throw new ArgumentNullException(nameof(details), "GpsLastPositionD cannot be null.");
        }

        var gpsMessage = new GpsLastPostionDto
        {
            Id = details.First().GpsLastPositionHId,
            GpsVendorId = vendor.Id,
            VendorName = vendor.VendorName,
            CreatedAt = vendor.CreatedAt,
            LastModified = vendor.LastModified,
            
            Data = details.Select(detail => new GpsLastPostionDetailDto
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