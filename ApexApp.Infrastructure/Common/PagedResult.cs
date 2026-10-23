namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Страница результатов запроса вместе с общим количеством элементов.
/// </summary>
/// <typeparam name="T">Тип элемента страницы.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Создаёт новую страницу результатов.
    /// </summary>
    /// <param name="items">Элементы текущей страницы.</param>
    /// <param name="totalCount">Общее количество элементов во всей выборке.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Элементы текущей страницы.
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Общее количество элементов во всей выборке.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Номер текущей страницы (начиная с 1).
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// Размер страницы.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Общее количество страниц.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// Признак наличия предыдущей страницы.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Признак наличия следующей страницы.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Создаёт пустую страницу результатов.
    /// </summary>
    public static PagedResult<T> Empty(int page, int pageSize) => new([], 0, page, pageSize);
}
