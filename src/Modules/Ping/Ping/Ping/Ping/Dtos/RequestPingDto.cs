namespace Ping.Ping.Dtos;

public class RequestPingDto
{
    public string Message { get; set; }
    public string? Url { get; set; } // Optional URL for HTTP pings


    [JsonConstructor]
    public RequestPingDto(string message, string? url = null)
    {
        Message = message;
        Url = url; // Initialize URL property
    }
    
}