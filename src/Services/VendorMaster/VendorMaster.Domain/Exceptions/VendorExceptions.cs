namespace VendorMaster.Domain.Exceptions;

// exception pas vendor gak ketemu di db
public class VendorNotFoundException : NotFoundException
{
    public VendorNotFoundException(Guid id) : base("GpsVendor", id) { }
    public VendorNotFoundException(string code) : base($"Vendor dengan kode '{code}' tidak ditemukan.") { }
}

// exception pas request client gak valid / duplikat
public class VendorBadRequestException : BadRequestException
{
    public VendorBadRequestException(string message, string? details = null) 
        : base(message, details ?? string.Empty)
    {
    }
}

// exception pas ada konflik unique key (misal vendor code udah ada)
public class VendorConflictException : ConflictException
{
    public VendorConflictException(string message) : base(message) { }
    public VendorConflictException(string message, string details) : base(message, details) { }
}
