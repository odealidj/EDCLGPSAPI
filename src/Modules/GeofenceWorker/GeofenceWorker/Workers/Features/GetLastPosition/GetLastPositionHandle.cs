
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Dtos;

namespace GeofenceWorker.Workers.Features.GetLastPosition;

public record GetLastPositionQuery(LastPositionRequestDto LastPosition)
    : IQuery<GetLastPositionResult>;
public record GetLastPositionResult(PaginatedResult<LastPositionResponseDto> LastPosition);


public class GetLastPositionQueryValidator : AbstractValidator<GetLastPositionQuery>
{
    public GetLastPositionQueryValidator()
    {
        RuleFor(x => x.LastPosition.PageIndex).NotEmpty().WithMessage("PageIndex is required");
        RuleFor(x => x.LastPosition.PageSize).NotEmpty().WithMessage("PageSize is required");
        
        RuleFor(x => x.LastPosition.PageIndex).GreaterThan(0).WithMessage("PageIndex must be greater than 0");
    }
}

public class GetLastPositionHandle(IGpsLastPositionRepository repository)
    : IQueryHandler<GetLastPositionQuery, GetLastPositionResult>
{
    public async Task<GetLastPositionResult> Handle(GetLastPositionQuery query, CancellationToken cancellationToken)
    {
        var lastPositions =  await repository.GetLastPositionAsync(
            query.LastPosition, cancellationToken: cancellationToken);

        var totalLastPositions = await repository.GetLastPositionCountAsync(
            query.LastPosition, cancellationToken);
        
       return new GetLastPositionResult(
            new PaginatedResult<LastPositionResponseDto>(
                query.LastPosition.PageIndex,
                query.LastPosition.PageSize,
                totalLastPositions,
                lastPositions)
        );
    }
}