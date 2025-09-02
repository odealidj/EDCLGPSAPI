
using System.Data;
using Dapper;
using Delivery.Data.Repositories.IRepositories;
using Delivery.Delivery.Dtos;
using Delivery.Delivery.Exceptions;
using Delivery.Delivery.Models;
using Microsoft.Data.SqlClient;

namespace Delivery.Data.Repositories;

public class DeliveryRepository(
    DeliveryDbContext? dbContext,
    ILogger<DeliveryRepository> logger)
    : IDeliveryRepository
{
    public async Task<Guid> UpsertDeliveryProgressAsync(DeliveryProgress deliveryProgress, CancellationToken cancellationToken = default)
    {
        try
        {
            if (dbContext == null) 
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            
            var dataDeliveryProgresses = await dbContext.DeliveryProgresses
                .FirstOrDefaultAsync(d => d.DeliveryNo == deliveryProgress.DeliveryNo, cancellationToken: cancellationToken);
            
            
            if (dataDeliveryProgresses != null)
            {
                //var dateTimeValue = DateTime.UtcNow;
                
                // Update jika ditemukan
                dataDeliveryProgresses.PlatNo = deliveryProgress.PlatNo;
                dataDeliveryProgresses.NoKtp = deliveryProgress.NoKtp;
                dataDeliveryProgresses.VendorName = deliveryProgress.VendorName;
                dataDeliveryProgresses.Lpcd = deliveryProgress.Lpcd;
                dataDeliveryProgresses.LastModifiedBy = !string.IsNullOrEmpty(deliveryProgress.LastModifiedBy) ? deliveryProgress.LastModifiedBy : "System";
                dataDeliveryProgresses.CreatedAt ??= DateTime.UtcNow;
                if (dataDeliveryProgresses.CreatedAt.Value.Kind != DateTimeKind.Utc)
                {
                    dataDeliveryProgresses.CreatedAt = DateTime.SpecifyKind(dataDeliveryProgresses.CreatedAt.Value, DateTimeKind.Utc);
                }
                dbContext.Update(dataDeliveryProgresses);
            }
            else
            {
                // Insert jika tidak ditemukan
                deliveryProgress.Id =Guid.NewGuid(); 
                deliveryProgress.CreatedAt = DateTime.UtcNow;
                deliveryProgress.CreatedBy =  !string.IsNullOrEmpty(deliveryProgress.CreatedBy) ? deliveryProgress.CreatedBy: "System";
                dbContext.Add(deliveryProgress);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return dataDeliveryProgresses?.Id ?? deliveryProgress.Id;
            
        }
        catch (DbUpdateException ex)
        { 
            logger.LogError(ex, "Terjadi kesalahan DbUpdateException saat menyimpan DeliveryProgress.");

            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new DeliveryProgressConflictException("DeliveryNo is already exists.", ex.Message);
            }
            throw new DeliveryProgressDatabaseAccessException("\nAn error occurred while saving data.", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan umum saat menyimpan DeliveryProgress.");
            throw new DeliveryProgressInternalServerException("An error occurred while saving data.", ex.Message);
        }
    }
    
}