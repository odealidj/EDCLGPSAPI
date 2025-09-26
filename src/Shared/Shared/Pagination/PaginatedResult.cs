namespace Shared.Pagination;

public class PaginatedResult<TEntity> where TEntity : class
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public long Count { get; set; }
    public bool Next { get; set; }
    public bool Prev { get; set; }
    public IEnumerable<TEntity> Data { get; set; } = [];

    public PaginatedResult() { }

    public PaginatedResult(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    {
        var normalizedPageIndex = pageIndex < 1 ? 1 : pageIndex;
        PageIndex = normalizedPageIndex;
        PageSize = pageSize;
        Count = count;
        Data = data;
        Next = (long)normalizedPageIndex * pageSize < count;
        Prev = normalizedPageIndex > 1;
    }

    public PaginatedResult(int pageIndex, int pageSize, long count, bool next, bool prev, IEnumerable<TEntity> data)
    {
        PageIndex = pageIndex < 1 ? 1 : pageIndex;
        PageSize = pageSize;
        Count = count;
        Next = next;
        Prev = prev;
        Data = data;
    }
}
