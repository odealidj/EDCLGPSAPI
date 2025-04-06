namespace Catalog.Products.Features.GetProducts;

public record GetProductsQuery(string Category)
    : IQuery<GetProductsResult>;
public record GetProductsResult(IEnumerable<ProductDto> Products);

public class GetProductsHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetProductsQuery, GetProductsResult>
{
    public async Task<GetProductsResult> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        // get products using dbContext
        // return result

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Category.Contains(query.Category))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        
        //mapping product entity to productdto
        //var productDtos = ProjectToProductDto(products);
        
        var productDtos = products.Adapt<List<ProductDto>>();

        return new GetProductsResult(productDtos);
    }

    /*
    // menghilankan ini dengan mapster
    private List<ProductDto> ProjectToProductDto(List<Product> products)
    {
        foreach (var product in products)
        {
            
        }

        return [];
    }
    */
}