using PawPoint.Services.Responses;
using Microsoft.EntityFrameworkCore;

namespace PawPoint.Services.Extensions;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        int maxPageSize = 50)
    {
        pageNumber = pageNumber switch
        {
            <= 0 => 1,
            _ => pageNumber
        };

        pageSize = pageSize switch
        {
            <= 0 => 1,
            _ when pageSize > maxPageSize => maxPageSize,
            _ => pageSize
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<T>(items, totalCount, totalPages, pageNumber, pageSize);
    }
}
