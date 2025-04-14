using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeofenceWorker.Data;
using GeofenceWorker.Workers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.Workers.Features;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;

    public Worker( IServiceScopeFactory scopeFactory, 
        HttpClient httpClient, 
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
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
                    .Include(v => v.Endpoint)
                    .Include(v => v.Auth)
                    .Where(v => v.Endpoint != null && v.Id == Guid.Parse("f466a15c-0b72-45ab-aa7f-8b35b18382ad"))
                    .ToListAsync(stoppingToken);

                foreach (var vendor in vendors)
                {
                    var request = new HttpRequestMessage
                    {
                        Method = new HttpMethod(vendor.Endpoint.Method),
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
                    if (vendor.Endpoint.Params != null)
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
                        if (vendor.Auth.Authtype == "Bearer")
                        {
                            // Get token from GpsVendorAuth BaseUrl
                            var authToken = await GetAuthTokenAsync(vendor.Auth);
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                        }
                        else if (vendor.Auth.Authtype == "Basic")
                        {
                            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{vendor.Auth.Headers?.ToString()}:{vendor.Auth.Params?.ToString()}"));
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                        }
                    }
                    
                    var response = await _httpClient.SendAsync(request, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Successfully called endpoint for Vendor {VendorName}: {Result}", vendor.VendorName, result);
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
                Content = new StringContent(auth.Bodies?.ToString() ?? "", Encoding.UTF8, "application/json")
            };
            
            // Add Headers from JsonObject if any
            if (auth.Headers != null)
            {
                foreach (var header in auth.Headers.AsObject())
                {
                    // Add each header from the JsonObject to the request headers
                    request.Headers.Add(header.Key, header.Value?.ToString());
                }
            }
                    
                    
            // Attach parameters to the URL if any
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
                _logger.LogError("Failed to get token for Vendor {VendorName}: {StatusCode} {ReasonPhrase}", auth.GpsVendor?.VendorName, tokenResponse.StatusCode, tokenResponse.ReasonPhrase);
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