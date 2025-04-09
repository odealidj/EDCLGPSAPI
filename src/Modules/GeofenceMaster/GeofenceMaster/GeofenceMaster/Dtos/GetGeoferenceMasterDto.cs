using Shared.Pagination;

namespace GeofenceMaster.GeofenceMaster.Dtos;

public record GetGeoferenceMasterDto(
    string VendorName, int PageIndex = 0, int PageSize = 10
    //PaginationRequest PaginationRequest 
);