namespace GeofenceMaster.GeofenceMaster.Dtos;

public class DeleteGeoferenceMasterDto
{
    public Guid Id { get; set; }

    public DeleteGeoferenceMasterDto()
    {
    }

    public DeleteGeoferenceMasterDto(Guid id)
    {
        Id = id;
    }
}

