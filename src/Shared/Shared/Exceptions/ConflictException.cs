using Microsoft.EntityFrameworkCore;

namespace Shared.Exceptions;

public class ConflictException: DbUpdateException
{
    public ConflictException(string message) 
        : base(message)
    {
    }

    public ConflictException(string message, string details) 
        : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}