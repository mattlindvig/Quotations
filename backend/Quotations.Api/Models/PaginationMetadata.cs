namespace Quotations.Api.Models;

public class PaginationMetadata
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public PaginationMetadata Pagination { get; set; } = new();
}
