using ApexApp.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за проверку аутентификации.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Тестовый метод для проверки авторизации пользовател и корректности выданного токена.
    /// </summary>
    [Authorize]
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

        return Ok(new LoginResponse(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("token_type").GetString()!,
            root.GetProperty("expires_in").GetInt32(),
            root.TryGetProperty("refresh_token", out var refreshToken)
                ? refreshToken.GetString()
                : null));
    }

    public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken);

    public sealed record LoginRequest(string Email, string Password);
}
