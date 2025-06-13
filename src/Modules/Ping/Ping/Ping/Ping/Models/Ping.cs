using Shared.DDD;

namespace Ping.Ping.Models;

public class Ping: Entity<Guid>
{
    // "Message" varchar(150),
    // "PingDate" timestamp default CURRENT_TIMESTAMP,
    // "Type" varchar(5),
    
    public string? Message { get; set; } 
    public DateTime? PingDate { get; set; }
    public string? Protocol { get; set; }

    public string? Url { get; set; }

    public Ping()
    {
        
    }
    public Ping(Guid id, string? message, DateTime pingDate, string? protocol, string? url)
    {
        Id = id;
        Message = message;
        PingDate = pingDate;
        Protocol = protocol;
        Url = url; // Initialize URL property
    }
}