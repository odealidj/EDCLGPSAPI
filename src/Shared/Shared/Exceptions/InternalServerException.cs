namespace Shared.Exceptions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:Update this implementation of 'ISerializable' to conform to the recommended serialization pattern", Justification = "BinaryFormatter is obsolete in .NET 8+")]
public class InternalServerException : Exception
{
    
    public InternalServerException(string message) 
        : base(message)
    {
    }

    public InternalServerException(string message, string details) 
        : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}
