using Delivery.Data;
using Delivery.Delivery.Dtos;
using Npgsql;

namespace Delivery.Delivery.Features.TrackDelivery;


public record TrackDeliveryEdclCommand(TrackDeliveryEdclRequestDto TrackDeliveryRequest)
    : ICommand<TrackDeliveryEdclResult>;
public record TrackDeliveryEdclResult(List<TrackDeliveryEdclResponseDto> TrackDeliveryResult);

internal class TrackDeliveryEdclHandler(DeliveryDbContext dbContext)
    : ICommandHandler<TrackDeliveryEdclCommand, TrackDeliveryEdclResult>
{
    public async Task<TrackDeliveryEdclResult> Handle(TrackDeliveryEdclCommand command, CancellationToken cancellationToken)
    {
        var sql = @"
        WITH FilteredData AS (
            SELECT
              1 AS ""Id"",
              trgde.""DeliveryNo"" AS delivery_no,
              trgde.""NoKtp"" AS no_ktp,
              trgde.""Lpcd"" AS lp_cd,
              trgde.""GpsVendorName"" AS gps_vendor,
              --'' AS flag_gps,
              trgde.""PlatNo"" AS plat_no,
              trgde.""DeviceId"" AS device_id,
              trgde.""Datetime"" AS datetime,
              trgde.""X"" AS x,
              trgde.""Y"" AS y,
              trgde.""Speed"" AS speed,
              trgde.""Course"" AS course,
              trgde.""StreetName"" AS street_name,
              ROW_NUMBER() OVER (ORDER BY MIN(trgde.""CreatedAt"")) AS rownum
            FROM
                edcl.tb_r_gps_delivery trgde
            WHERE
                trgde.""DeliveryNo"" = @p_delivery_no
            GROUP BY
              trgde.""DeliveryNo"",
              trgde.""NoKtp"",
              trgde.""Lpcd"",
              trgde.""GpsVendorName"",
              trgde.""PlatNo"",
              trgde.""DeviceId"",
              trgde.""Datetime"",
              trgde.""X"",
              trgde.""Y"",
              trgde.""Speed"",
              trgde.""Course"",
              trgde.""StreetName""
        )
        SELECT
            ""Id"",
            rownum AS positionid,
            delivery_no AS DeliveryNo,
            no_ktp AS NoKTP,
            lp_cd AS LPCD,
            gps_vendor AS GpsVendor,
            '' AS FlagGps,
            plat_no AS PlatNo,
            device_id AS DeviceId,
            datetime AS Datetime,
            x AS X,
            y AS Y,
            speed AS Speed,
            course AS Course,
            street_name AS StreetName,
            rownum AS RowNum
        FROM
            FilteredData
        WHERE
            @p_density = 1 OR rownum % @p_density = 1
        ORDER BY
            rownum;
    ";

        var gpsDeliveries = await dbContext.GpsDeliveries
            .FromSqlRaw(sql, 
                new NpgsqlParameter("p_delivery_no",command.TrackDeliveryRequest.DeliveryNo), 
                new NpgsqlParameter("p_density", command.TrackDeliveryRequest.Density))
            .ToListAsync(cancellationToken: cancellationToken);
        
        var i =1;
        var resultDto = gpsDeliveries.Select(gps => new TrackDeliveryEdclResponseDto
        {
            PositionId = i,
            DeliveryNo = command.TrackDeliveryRequest.DeliveryNo,
            NoKTP = gps.NoKtp ?? string.Empty,
            LPCD = gps.Lpcd ?? string.Empty,
            GpsVendor = gps.GpsVendorName,
            FlagGps = string.Empty,
            PlatNo = gps.PlatNo?? string.Empty,
            DeviceId = gps.DeviceId?? string.Empty,
            Datetime = gps.Datetime,
            X = gps.X,
            Y = gps.Y,
            Speed = gps.Speed,
            Course = gps.Course,
            StreetName = !string.IsNullOrEmpty(gps.StreetName)?gps.StreetName:string.Empty
        }).ToList();
        
        return new TrackDeliveryEdclResult(resultDto);
    }
}