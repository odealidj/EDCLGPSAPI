namespace Healthy.Healthy.Dtos;

public class HttpPingRequestDto
{
    public string? Url { get; set; } = string.Empty;

    public HttpPingRequestDto()
    {
        
    }

    public HttpPingRequestDto(string? url = "")
    {
        Url = url;
    }
}