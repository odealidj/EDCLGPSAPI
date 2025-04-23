using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeofenceWorker.Data;
using GeofenceWorker.Workers.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Shared.Messaging.Events;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope()) // Create a scope for DbContext
            {
                var context = scope.ServiceProvider.GetRequiredService<GeofenceWorkerDbContext>();
                
                // Get all vendors that need to call endpoints
                var vendors = await context.GpsVendors
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
        }
    }

    private async Task ProcessCombinedEndpoints(GpsVendor vendor, List<GpsVendorEndpoint> endpoints, IServiceScope scope,
        GeofenceWorkerDbContext context, CancellationToken stoppingToken)
    {
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
                foreach (var param in parameters.AsObject())
                {
                    request.RequestUri = new UriBuilder(request.RequestUri)
                    {
                        Query = $"{param.Key}={param.Value?.ToString()}"
                    }.Uri;
                }
            }
        
            // Set Authorization Header if required
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
        
            var response = await _httpClient.SendAsync(request, stoppingToken);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync(stoppingToken);
                var dataMapping = GetDataItems(responseData, "vin");
            }
            else
            {
                _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", endpoint.GpsVendor.VendorName, response.StatusCode, response.ReasonPhrase);
            }
        }
    }

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
        
        // Set Authorization Header if required
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
        
        var response = await _httpClient.SendAsync(request, stoppingToken);
        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync();
            
            
            var responseMapping = await ProcessVendorResponse(endpoint.GpsVendor ,responseData, context);

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
            
            _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}: {ResponseData}", endpoint.GpsVendor.VendorName, responseData);
            
            
            // Pass the context to MapResponseToDatabase method
            ////var mappedData = await MapResponseToDatabase(responseData, vendor.Id, context);

            // Process the mapped data
            ////_logger.LogInformation("Successfully mapped data for Vendor {VendorName}: {MappedData}", vendor.VendorName, mappedData);                    
        }
        else
        {
            _logger.LogError("Failed to call endpoint for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", endpoint.GpsVendor.VendorName, response.StatusCode, response.ReasonPhrase);
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
    public class TestMessage
    {
        public string Text { get; set; }
    }
    
    public class ListGpsLastPosition
    {
        public List<GpsLastPosition> GpsLastPositions { get; set; }
    }
    
    
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
    
    private async Task<List<GpsLastPosition>> ProcessVendorResponse(GpsVendor gpsVendor, string jsonResponse, GeofenceWorkerDbContext _context)
    {
        // 1. Ambil semua mapping untuk vendor
        var mappings = await _context.Mappings
            .Where(v => v.GpsVendorId == gpsVendor.Id)
            .ToListAsync();

        var  dataItems = GetDataItems(jsonResponse, mappings.First().DataPath ?? string.Empty);

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
    
    
    private GpsLastPosition CreateNewGpsLastPosition(GpsLastPosition gpsLastPosition)
    {
        var data = GpsLastPosition.Create(
            Guid.NewGuid(),
            gpsLastPosition.GpsVendorId,
            gpsLastPosition.Lpcd,
            gpsLastPosition.PlatNo ?? string.Empty,
            gpsLastPosition.DeviceId ?? string.Empty,
            gpsLastPosition.Datetime,
            gpsLastPosition.X,
            gpsLastPosition.Y,
            gpsLastPosition.Speed,
            gpsLastPosition.Course,
            gpsLastPosition.StreetName?? String.Empty
            );

        return data;
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
            if (auth.Params != null)
            {
                var parameters = auth.Params;
                foreach (var param in parameters.AsObject())
                {
                    request.RequestUri = new UriBuilder(request.RequestUri)
                    {
                        Query = $"{param.Key}={param.Value?.ToString()}"
                    }.Uri;
                }
            }
            */
            
            
            if (!string.IsNullOrEmpty(auth.Username) && !string.IsNullOrEmpty(auth.Password))
            {
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
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

    // Helper method to extract the value from JSON based on the ResponsePath
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

}

public class VendorMapper
{
    public static string GetMappedValue(string jsonResponse, string jsonPath)
    {
        var jsonObject = JObject.Parse(jsonResponse);
        return jsonObject.SelectToken(jsonPath)?.ToString();
    }
}