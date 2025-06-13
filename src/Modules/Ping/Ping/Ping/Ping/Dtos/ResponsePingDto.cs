namespace Ping.Ping.Dtos;

public class ResponsePingDto
{
    public Guid Id { get; set; }
    public string? Message { get; set; }
    public DateTime? PingDate { get; set; }
    public string? Protocol { get; set; }
    [JsonConstructor]
    public ResponsePingDto(Guid id, string? message, DateTime? pingDate, string? protocol)
    {
        Id = id;
        Message = message;
        PingDate = pingDate;
        Protocol = protocol;
    }
}