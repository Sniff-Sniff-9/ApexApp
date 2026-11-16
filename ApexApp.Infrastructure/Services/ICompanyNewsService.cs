using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>Company news feed and administrator CRUD.</summary>
public interface ICompanyNewsService
{
    Task<PagedResult<CompanyNewsView>> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CompanyNewsView?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompanyNewsView>> CreateAsync(
        Guid authorId,
        CompanyNewsInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompanyNewsView>> UpdateAsync(
        long id,
        CompanyNewsInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

/// <summary>Fields that can be edited on a company-news item.</summary>
public sealed record CompanyNewsInput(
    string Title,
    string? Summary,
    string Content,
    string? ImageUrl);
