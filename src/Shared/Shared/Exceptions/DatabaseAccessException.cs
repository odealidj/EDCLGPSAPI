using Microsoft.EntityFrameworkCore;

namespace Shared.Exceptions;

public class DatabaseAccessException: DbUpdateException
{
    public DatabaseAccessException(string message) 
        : base(message)
    {
    }

    public DatabaseAccessException(string message, string details) 
        : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}