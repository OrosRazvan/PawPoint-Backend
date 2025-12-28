namespace PawPoint.Services.Responses
{
    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int TotalPages,
        int PageNumber,
        int PageSize);
}
