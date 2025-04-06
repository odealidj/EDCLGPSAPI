namespace Catalog.Products.Features.GetProductById;

public record GetProductByIdQuery(Guid Id)
    : IQuery<GetProductByIdResult>;
public record GetProductByIdResult(ProductDto Product);

public class GetProductByIdHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResult>
{
    public async Task<GetProductByIdResult> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        // get products by id using dbContext
        // return result

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        
        if (product is null)
        {
            throw new Exception($"Prodcut not found: {query.Id}");
        }

        //mapping product entity to productdto
        var productDto = product.Adapt<ProductDto>();

        return new GetProductByIdResult(productDto);
    }
}