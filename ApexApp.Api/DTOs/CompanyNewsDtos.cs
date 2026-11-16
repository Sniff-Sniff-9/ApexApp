namespace ApexApp.Api.DTOs;

/// <summary>Create/update payload for company news.</summary>
public sealed record UpsertCompanyNewsRequest(
    string Title,
    string? Summary,
    string Content,
    string? ImageUrl);

/// <summary>Company news feed item.</summary>
public sealed record CompanyNewsDto(
    long Id,
    string Title,
    string? Summary,
    string Content,
    string? ImageUrl,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
