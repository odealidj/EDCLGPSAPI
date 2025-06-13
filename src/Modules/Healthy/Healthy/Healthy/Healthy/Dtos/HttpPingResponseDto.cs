namespace Healthy.Healthy.Dtos;

public class HttpPingResponseDto(string message, DateTime timestamp, string protocol, string url )
{
    // 	"Message" varchar(150) NULL,
    // "PingDate" timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    // "Protocol" varchar(5) NULL,
    
    
    public string Message { get; set; } = message;
    public DateTime Timestamp { get; set; } = timestamp;
    public string Protocol { get; set; } = protocol;
    public string Url { get; set; } = url;

    public HttpPingResponseDto() : this(string.Empty, DateTime.UtcNow, string.Empty,string.Empty)
    {
    }
}