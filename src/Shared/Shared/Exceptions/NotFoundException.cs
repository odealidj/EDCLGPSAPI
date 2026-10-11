namespace Shared.Exceptions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:Update this implementation of 'ISerializable' to conform to the recommended serialization pattern", Justification = "BinaryFormatter is obsolete in .NET 8+")]
public class NotFoundException : Exception
{
    public NotFoundException(string message) 
        : base(message)
    {
    }

    public NotFoundException(string name, object key) 
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
