using Shared.DDD;

namespace Healthy.Healthy.Models;

public class Ping: Entity<Guid>
{

    public string? Message { get; set; } 
    public DateTime? PingDate { get; set; }
    public string? Protocol { get; set; }
    public string? Url { get; set; } // Added URL property for HTTP pings

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
    
    public static Ping Create(Guid id, string? message, ////string lpcdId , 
        DateTime? pingDate, string? protocol, string? url)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);

        var ping = new Ping()
        {
            Id = id,
            Message = message,
            PingDate = pingDate ?? DateTime.Now,
            Protocol = protocol, // Default to HTTP if not provided
            Url = url // Initialize URL property
        };

        return ping;
    }
}