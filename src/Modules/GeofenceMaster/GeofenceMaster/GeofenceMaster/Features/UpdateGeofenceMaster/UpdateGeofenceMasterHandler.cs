using GeofenceMaster.GeofenceMaster.Dtos;
using GeofenceMaster.GeofenceMaster.Exceptions;
using GeofenceMaster.GeofenceMaster.Models;
using Shared.Contracts.CQRS;

namespace GeofenceMaster.GeofenceMaster.Features.UpdateGeofenceMaster;

public record UpdateGeofenceMasterCommand(GeofenceMasterDto GeofenceMaster)
    : ICommand<UpdateGeofenceMasterResult>;
public record UpdateGeofenceMasterResult(bool IsSuccess);

public class UpdateGeofenceMasterCommandValidator : AbstractValidator<UpdateGeofenceMasterCommand>
{
    public UpdateGeofenceMasterCommandValidator()
    {
        RuleFor(x => x.GeofenceMaster.Id).NotEmpty().WithMessage("Id Vendor is required");
        RuleFor(x => x.GeofenceMaster.VendorName).NotEmpty().WithMessage("Vendor Name is required");
        ////RuleFor(x => x.GeofenceMaster.LpcdId).NotEmpty().WithMessage("LPCD is required");
    }
}

public class UpdateGeofenceMasterHandler(GeofenceMasterDbContext dbContext)
    : ICommandHandler<UpdateGeofenceMasterCommand, UpdateGeofenceMasterResult>
{
    public async Task<UpdateGeofenceMasterResult> Handle(UpdateGeofenceMasterCommand command, CancellationToken cancellationToken)
    {
        var lpcdValuesToCheck = command.GeofenceMaster.Lpcds
            .Where(x => !string.IsNullOrWhiteSpace(x.Lpcd))
            .Select(x => x.Lpcd.Trim().ToLower())
            .ToList();

        if (!lpcdValuesToCheck.Any())
        {
            throw new GeofenceMasterBadRequestException("LPCD not found", "LPCD not found");

        }
        
        
        // Validasi duplikasi lpcd
        var duplicateLpcds = command.GeofenceMaster.Lpcds
            .GroupBy(lpcd => lpcd.Lpcd) 
            .Where(group => group.Count() > 1) 
            .Select(group => group.Key) 
            .ToList();

        if (duplicateLpcds.Count > 0)
        {
            throw new GeofenceMasterBadRequestException($"Duplicate LPCD(s) found: {string.Join(", ", duplicateLpcds)}","Duplicate LPCD");
        }
        
        var duplicateLpcdIds = command.GeofenceMaster.Lpcds
            .Where(lpcd => lpcd.Id != Guid.Empty)
            .GroupBy(lpcd => lpcd.Id) 
            .Where(group => group.Count() > 1) 
            .Select(group => group.Key) 
            .ToList();

        if (duplicateLpcdIds.Count > 0 )
        {
            throw new GeofenceMasterBadRequestException($"Duplicate LPCD(s) Id found: {string.Join(", ", duplicateLpcdIds)}","Duplicate LPCD Id");
        }
        
        var duplicateEndPointUrls = command.GeofenceMaster.GeofenceMasterEndpoints
            .GroupBy(endpoint => endpoint.BaseUrl) 
            .Where(group => group.Count() > 1) 
            .Select(group => group.Key) 
            .ToList();

        if (duplicateEndPointUrls.Count > 0)
        {
            throw new GeofenceMasterBadRequestException($"Duplicate EndPoint Url found: {string.Join(", ", duplicateEndPointUrls)}","Duplicate EndPoint Url");
        }
        
        var duplicateEndPointIds = command.GeofenceMaster.GeofenceMasterEndpoints
            .Where(endpoint => endpoint.Id != Guid.Empty)
            .GroupBy(endpoint => endpoint.Id) 
            .Where(group => group.Count() > 1) 
            .Select(group => group.Key) 
            .ToList();

        if (duplicateEndPointIds.Count > 0)
        {
            throw new GeofenceMasterBadRequestException($"Duplicate EndPoint Id(s) found: {string.Join(", ", duplicateEndPointIds)}","Duplicate EndPoint Id");
        }
        
        
        /*
        var matchesInDb = await dbContext.Lpcds
            .AsNoTracking()
            .Where(lpcd =>
                lpcd.GpsVendorId == command.GeofenceMaster.Id &&
                lpcdValuesToCheck.Contains(lpcd.Lpcd.ToLower()))
            .ToListAsync(cancellationToken);

        if (matchesInDb.Count > 0)
        {
            throw new GeofenceMasterBadRequestException($"Duplicate EndPoint Id(s) found: {string.Join(", ", matchesInDb)}","Duplicate LPCD");
        }
        */
        
        var geofenceMasters = await dbContext.GpsVendors
            .Where(x => x.Id == command.GeofenceMaster.Id )
            .Include(x=> x.GpsVendorEndpoints)
            .Include( x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include( x => x.Lpcds)
            .ToListAsync(cancellationToken);

        if (geofenceMasters.Count == 0)
        {
            if (command.GeofenceMaster.Id != null)
                throw new GeofenceMasterNotFoundException(command.GeofenceMaster.Id.Value);
        }
        
        //Step 1: remove GpsVendorLpcds yang ada
        ////dbContext.GpsVendorLpcds.RemoveRange(geofenceMasters.First().GpsVendorLpcds.Distinct());
        
        // Step 2: Update GpsVendor
        geofenceMasters.First().VendorName = command.GeofenceMaster.VendorName;
        geofenceMasters.First().Timezone = command.GeofenceMaster.Timezone;
        geofenceMasters.First().RequiredAuth = command.GeofenceMaster.RequiredAuth;
        geofenceMasters.First().AuthType = command.GeofenceMaster.AuthType;
        geofenceMasters.First().Username = command.GeofenceMaster.Username;
        geofenceMasters.First().Password = command.GeofenceMaster.Password;
        geofenceMasters.First().ProcessingStrategy = command.GeofenceMaster.ProcessingStrategy;
        geofenceMasters.First().ProcessingStrategyPathData = command.GeofenceMaster.ProcessingStrategyPathData;
        geofenceMasters.First().ProcessingStrategyPathKey = command.GeofenceMaster.ProcessingStrategyPathKey;
        
        // Step 3: Update atau tambahkan GpsVendorEndpoint berdasarkan Items di GeofenceMasterDto

        if (command.GeofenceMaster.GeofenceMasterEndpoints.Count > 0)
        {
            var endPointIds = command.GeofenceMaster.GeofenceMasterEndpoints
                .Where(endpoint => endpoint.Id != Guid.Empty) // Filter untuk menghilangkan Guid.Empty
                .Select(endpoint => endpoint.Id)
                .ToList();
            
            if (endPointIds.Count > 0)
            {
                var endPointIdsToDelete =  await dbContext.GpsVendorEndpoints
                    .Where(tmgvl => !endPointIds.Contains(tmgvl.Id) && tmgvl.GpsVendorId == command.GeofenceMaster.Id)
                    .Select(tmgvl => tmgvl.Id)
                    .ToListAsync(cancellationToken: cancellationToken);
                
                if (endPointIdsToDelete.Count > 0)
                {
                    // Hapus endpoint yang tidak ada di command
                    await dbContext.GpsVendorEndpoints
                        .Where(endpoint  => endPointIdsToDelete.Contains(endpoint.Id) && endpoint.GpsVendorId == command.GeofenceMaster.Id)
                        .ExecuteDeleteAsync(cancellationToken);            
                }
            }

            foreach (var itemDto in command.GeofenceMaster.GeofenceMasterEndpoints)
            {
                geofenceMasters.First().AddGpsVendorEndpoint(
                    itemDto.Id,
                    geofenceMasters.First().Id,
                    itemDto.BaseUrl,
                    itemDto.Method,
                    itemDto.ContentType,
                    itemDto.Headers,
                    itemDto.Params,
                    itemDto.Bodies,
                    itemDto.VarParams,
                    itemDto.MaxPath
                );
            } 
        }
        else
        {
            await dbContext.GpsVendorEndpoints
                .Where(endPoint => endPoint.GpsVendorId == command.GeofenceMaster.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
            
        
        if (command.GeofenceMaster.GeofenceMasterAuth != null)
        {
            var gpsVendorAuth = geofenceMasters.First().GpsVendorAuth;
            if (gpsVendorAuth != null)
            {
                geofenceMasters.First().AddGpsVendorAuth(
                    command.GeofenceMaster.GeofenceMasterAuth.Id,
                    geofenceMasters.First().Id,
                    command.GeofenceMaster.GeofenceMasterAuth.BaseUrl,
                    command.GeofenceMaster.GeofenceMasterAuth.Method,
                    command.GeofenceMaster.GeofenceMasterAuth.Authtype,
                    command.GeofenceMaster.GeofenceMasterAuth.ContentType,
                    command.GeofenceMaster.GeofenceMasterAuth.Username,
                    command.GeofenceMaster.GeofenceMasterAuth.Password,
                    command.GeofenceMaster.GeofenceMasterAuth.TokenPath,
                    command.GeofenceMaster.GeofenceMasterAuth.Headers,
                    command.GeofenceMaster.GeofenceMasterAuth.Params,
                    command.GeofenceMaster.GeofenceMasterAuth.Bodies);
            }
            else
            {
                geofenceMasters.First().AddGpsVendorAuth(
                    Guid.Empty,
                    geofenceMasters.First().Id,
                    command.GeofenceMaster.GeofenceMasterAuth.BaseUrl,
                    command.GeofenceMaster.GeofenceMasterAuth.Method,
                    command.GeofenceMaster.GeofenceMasterAuth.Authtype,
                    command.GeofenceMaster.GeofenceMasterAuth.ContentType,
                    command.GeofenceMaster.GeofenceMasterAuth.Username,
                    command.GeofenceMaster.GeofenceMasterAuth.Password,
                    command.GeofenceMaster.GeofenceMasterAuth.TokenPath,
                    command.GeofenceMaster.GeofenceMasterAuth.Headers,
                    command.GeofenceMaster.GeofenceMasterAuth.Params,
                    command.GeofenceMaster.GeofenceMasterAuth.Bodies);
                
                dbContext.Add(geofenceMasters.First().GpsVendorAuth);
            }
        }
        else
        {
            await dbContext.GpsVendorAuths
                .Where(auth => auth.GpsVendorId == command.GeofenceMaster.Id)
                .ExecuteDeleteAsync(cancellationToken);
            
        }
        
        foreach (var itemDto in command.GeofenceMaster.GeofenceMasterMappings)
        {
            geofenceMasters.First().AddMapping(
                itemDto.Id,
                geofenceMasters.First().Id,
                itemDto.ResponseField,
                itemDto.MappedField);
        }


        if (command.GeofenceMaster.Lpcds.Count > 0)
        {

            var lpcdIds = command.GeofenceMaster.Lpcds
                .Where(lpcd => lpcd.Id != Guid.Empty) // Filter untuk menghilangkan Guid.Empty
                .Select(lpcd => lpcd.Id)
                .ToList();

            if (lpcdIds.Count > 0)
            {

                var idsToDelete = await dbContext.Lpcds
                    ////.AsNoTracking()
                    .Where(tmgvl => !lpcdIds.Contains(tmgvl.Id) && tmgvl.GpsVendorId == command.GeofenceMaster.Id)
                    .Select(tmgvl => tmgvl.Id)
                    .ToListAsync(cancellationToken: cancellationToken);
                
                if (idsToDelete.Count > 0)
                {
                    // Hapus LPCD yang tidak ada di command
                    await dbContext.Lpcds
                        .Where(lpcd => idsToDelete.Contains(lpcd.Id) && lpcd.GpsVendorId == command.GeofenceMaster.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                }

            }

            var lpcdList = command.GeofenceMaster.Lpcds
                .Where(lpcd => lpcd.Id == Guid.Empty) // Filter untuk menghilangkan Guid.Empty
                .Select(lpcd => lpcd.Lpcd)
                .ToList();

            if (lpcdList.Count > 0)
            {
                var lToDelete = await dbContext.Lpcds
                    ////.AsNoTracking()
                    .Where(l => lpcdList.Contains(l.Lpcd) && l.GpsVendorId == command.GeofenceMaster.Id)
                    .Select(l => l.Lpcd)
                    .ToListAsync(cancellationToken: cancellationToken);
                
                if (lToDelete.Count > 0)
                {
                    // Hapus LPCD yang tidak ada di command
                    await dbContext.Lpcds
                        .Where(lpcd => lToDelete.Contains(lpcd.Lpcd) && lpcd.GpsVendorId == command.GeofenceMaster.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                }
            }

            foreach (var lpcd in command.GeofenceMaster.Lpcds)
            {
                geofenceMasters.First().AddLpcd(
                    lpcd.Id==Guid.Empty?Guid.NewGuid(): lpcd.Id,
                    geofenceMasters.First().Id,
                    lpcd.Lpcd
                );
       
            }
            
            var lpcds = geofenceMasters.First().Lpcds;
            
            foreach (var item in lpcds)
            {
                var existingInDb = await dbContext.Lpcds
                    //.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken);

                if (existingInDb == null)
                {
                    // Ini adalah data baru
                    dbContext.Lpcds.Add(item);
                }
                
                else
                {
                    // Ini adalah data lama
                    dbContext.Lpcds.Update(item);
                }
                
            }
        }
        else
        {
            await dbContext.Lpcds
                .Where(lpcd => lpcd.GpsVendorId == command.GeofenceMaster.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }


        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateGeofenceMasterResult(true);
        

    }
    
    
}