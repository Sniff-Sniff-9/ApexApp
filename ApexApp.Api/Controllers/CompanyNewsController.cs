using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>Company news feed and administrator CRUD.</summary>
[ApiController]
[Route("api/company-news")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class CompanyNewsController(
    ICompanyNewsService companyNewsService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        var result = await companyNewsService.GetAsync(
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<CompanyNewsDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var news = await companyNewsService.GetByIdAsync(
            id,
            cancellationToken);

        return news is null
            ? NotFound(new { message = "News was not found." })
            : Ok(ToDto(news));
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageNews)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertCompanyNewsRequest request,
        CancellationToken cancellationToken)
    {
        var authorId = User.GetUserId();

        if (authorId is null)
        {
            return Unauthorized();
        }

        var result = await companyNewsService.CreateAsync(
            authorId.Value,
            ToInput(request),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var dto = ToDto(result.Value!);

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AppPolicies.ManageNews)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpsertCompanyNewsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyNewsService.UpdateAsync(
            id,
            ToInput(request),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = AppPolicies.ManageNews)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await companyNewsService.DeleteAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    private static CompanyNewsInput ToInput(UpsertCompanyNewsRequest request) =>
        new(
            request.Title,
            request.Summary,
            request.Content,
            request.ImageUrl);

    private static CompanyNewsDto ToDto(CompanyNewsView view) =>
        new(
            view.News.Id,
            view.News.Title,
            view.News.Summary,
            view.News.Content,
            view.News.ImageUrl,
            view.News.AuthorId,
            view.AuthorName,
            view.AuthorAvatarUrl,
            view.News.CreatedAt,
            view.News.UpdatedAt);

    private static string? ValidatePaging(int page, int pageSize) =>
        page < 1
            ? "Page must be greater than or equal to 1."
            : pageSize is < 1 or > MaxPageSize
                ? $"PageSize must be between 1 and {MaxPageSize}."
                : null;
}
