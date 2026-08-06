using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Common;

public class PaginationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor que cero.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Puedes solicitar entre 1 y 100 resultados por página.")]
    public int PageSize { get; set; } = 24;
}

public sealed class PagedResponse<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }

    public static PagedResponse<T> Create(
        IEnumerable<T> items,
        int page,
        int pageSize,
        int totalItems) => new()
        {
            Items = items.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
}
