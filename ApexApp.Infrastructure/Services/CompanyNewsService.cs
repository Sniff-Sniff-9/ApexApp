using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Company news feed. Creation is publication; the current schema has no draft
/// state. News is intentionally not fanned out into notifications.
/// </summary>
public sealed class CompanyNewsService(
    AppDbContext dbContext,
    ICacheService cache) : ICompanyNewsService
{
    private const int TitleMaxLength = 200;
    private const int SummaryMaxLength = 500;
    private const int ContentMaxLength = 50_000;

    public async Task<PagedResult<CompanyNewsView>> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var cacheKey = CacheKeys.CompanyNews(page, pageSize);

        if (cache.TryGet<PagedResult<CompanyNewsView>>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var query = dbContext.CompanyNews
            .AsNoTracking()
            .Include(news => news.Author);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(news => news.CreatedAt)
            .ThenByDescending(news => news.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<CompanyNewsView>(
            items.Select(ToView).ToList(),
            totalCount,
            page,
            pageSize);

        cache.Set(cacheKey, result, CacheKeys.CompanyNewsTtl);

        return result;
    }

    public async Task<CompanyNewsView?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var news = await dbContext.CompanyNews
            .AsNoTracking()
            .Include(item => item.Author)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return news is null ? null : ToView(news);
    }

    public async Task<ServiceResult<CompanyNewsView>> CreateAsync(
        Guid authorId,
        CompanyNewsInput input,
        CancellationToken cancellationToken = default)
    {
        if (authorId == Guid.Empty)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, "A valid author ID is required.");
        }

        if (input is null)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, "A news payload is required.");
        }

        var validationError = Validate(input);

        if (validationError is not null)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, validationError);
        }

        var authorExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == authorId, cancellationToken);

        if (!authorExists)
        {
            return ServiceResult<CompanyNewsView>.Fail(404, "Author was not found.");
        }

        var now = DateTimeOffset.UtcNow;

        var news = new CompanyNews
        {
            Title = input.Title.Trim(),
            Summary = NormalizeOptional(input.Summary),
            Content = input.Content.Trim(),
            ImageUrl = NormalizeOptional(input.ImageUrl),
            AuthorId = authorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.CompanyNews.Add(news);
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidate();

        var view = await GetByIdAsync(news.Id, cancellationToken);

        return view is null
            ? ServiceResult<CompanyNewsView>.Fail(404, "News was not found.")
            : ServiceResult<CompanyNewsView>.Ok(view);
    }

    public async Task<ServiceResult<CompanyNewsView>> UpdateAsync(
        long id,
        CompanyNewsInput input,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, "A valid news ID is required.");
        }

        if (input is null)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, "A news payload is required.");
        }

        var validationError = Validate(input);

        if (validationError is not null)
        {
            return ServiceResult<CompanyNewsView>.Fail(400, validationError);
        }

        var news = await dbContext.CompanyNews
            .Include(item => item.Author)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (news is null)
        {
            return ServiceResult<CompanyNewsView>.Fail(404, "News was not found.");
        }

        news.Title = input.Title.Trim();
        news.Summary = NormalizeOptional(input.Summary);
        news.Content = input.Content.Trim();
        news.ImageUrl = NormalizeOptional(input.ImageUrl);
        news.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidate();

        return ServiceResult<CompanyNewsView>.Ok(ToView(news));
    }

    public async Task<ServiceResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ServiceResult.Fail(400, "A valid news ID is required.");
        }

        var news = await dbContext.CompanyNews
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (news is null)
        {
            return ServiceResult.Fail(404, "News was not found.");
        }

        dbContext.CompanyNews.Remove(news);
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidate();

        return ServiceResult.Ok();
    }

    private static string? Validate(CompanyNewsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return "Title is required.";
        }

        if (input.Title.Trim().Length > TitleMaxLength)
        {
            return $"Title must not exceed {TitleMaxLength} characters.";
        }

        if (input.Summary?.Trim().Length > SummaryMaxLength)
        {
            return $"Summary must not exceed {SummaryMaxLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(input.Content))
        {
            return "Content is required.";
        }

        if (input.Content.Trim().Length > ContentMaxLength)
        {
            return $"Content must not exceed {ContentMaxLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            var imageUrl = input.ImageUrl.Trim();

            if (!imageUrl.StartsWith('/') &&
                (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return "ImageUrl must be an absolute HTTP(S) URL or an app-relative path.";
            }
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CompanyNewsView ToView(CompanyNews news)
    {
        var authorName = news.Author is null
            ? string.Empty
            : $"{news.Author.FirstName} {news.Author.LastName}".Trim();

        return new CompanyNewsView(
            news,
            authorName,
            news.Author?.AvatarUrl);
    }

    private void Invalidate() =>
        cache.RemoveByPrefix(CacheKeys.CompanyNewsPrefix);
}
