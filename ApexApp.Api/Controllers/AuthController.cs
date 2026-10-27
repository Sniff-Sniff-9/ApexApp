using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за проверку аутентификации.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthController(
    AppDbContext dbContext,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Тестовый метод для проверки авторизации пользовател и корректности выданного токена.
    /// </summary>
    /// <remarks>
    /// Используется та же политика, что и в остальных защищённых методах,
    /// поэтому токен без утверждения <c>is_active</c> или с <c>is_active=false</c> отклоняется.
    /// </remarks>
    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            message = "You are authenticated!",
            userId         
        });
    }

    /// <summary>
    /// Выдача токена по почте и паролю.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
[FromBody] LoginRequest request,
[FromServices] IHttpClientFactory httpClientFactory,
IConfiguration configuration,
CancellationToken cancellationToken)
    {
        var supabaseUrl = configuration["Supabase:ProjectUrl"];
        var supabaseKey = configuration["Supabase:AnonKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) ||
            string.IsNullOrWhiteSpace(supabaseKey))
        {
            return StatusCode(500, new { message = "Supabase configuration is missing." });
        }

        var client = httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.Add("apikey", supabaseKey);

        using var response = await client.PostAsJsonAsync(
            $"{supabaseUrl.TrimEnd('/')}/auth/v1/token?grant_type=password",
            new { email = request.Email, password = request.Password },
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        using var document = System.Text.Json.JsonDocument.Parse(content);
        var root = document.RootElement;

        using var userRequest = new HttpRequestMessage(
    HttpMethod.Get,
    $"{supabaseUrl.TrimEnd('/')}/auth/v1/user");

        userRequest.Headers.TryAddWithoutValidation("apikey", supabaseKey);
        userRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                root.GetProperty("access_token").GetString());

        using var userResponse = await client.SendAsync(
            userRequest, cancellationToken);

        if (!userResponse.IsSuccessStatusCode)
        {
            return StatusCode(502, new
            {
                message = "Could not retrieve user from Supabase."
            });
        }

        using var userDocument = System.Text.Json.JsonDocument.Parse(
            await userResponse.Content.ReadAsStringAsync(cancellationToken));

        var userId = userDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return StatusCode(500, new
            {
                message = "User profile was not found."
            });
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = root.GetProperty("access_token").GetString()!;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        var userRole = jwt.Claims
            .FirstOrDefault(c => c.Type == "user_role")
            ?.Value;

        Console.WriteLine($"User role from JWT: {userRole ?? "not found"}");

        return Ok(new LoginResponse(
            accessToken,
            root.GetProperty("token_type").GetString()!,
            root.GetProperty("expires_in").GetInt32(),
            root.TryGetProperty("refresh_token", out var refreshToken)
                ? refreshToken.GetString()
                : null,
            user.MustChangePassword,
            userRole));
    }

    /// <summary>
    /// Смена пароля.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize(Policy = AppPolicies.ActiveUser)]
    public async Task<IActionResult> ChangePassword(
    [FromBody] ChangePasswordRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "New password is required." });
        }

        var userIdValue = User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var accessToken = Request.Headers.Authorization
            .ToString()
            .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        var supabaseUrl = _configuration["Supabase:ProjectUrl"];
        var anonKey = _configuration["Supabase:AnonKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) ||
            string.IsNullOrWhiteSpace(anonKey) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            return StatusCode(500, new
            {
                message = "Supabase configuration or access token is missing."
            });
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("apikey", anonKey);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

        using var response = await client.PutAsJsonAsync(
            $"{supabaseUrl.TrimEnd('/')}/auth/v1/user",
            new { password = request.NewPassword },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return BadRequest(new
            {
                message = "Supabase could not update the password."
            });
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User profile was not found." });
        }

        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Password changed successfully." });
    }


    public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken,
    bool MustChangePassword,
    string? UserRole);

    public sealed record LoginRequest(string Email, string Password);

    public sealed record ChangePasswordRequest(string NewPassword);
}
