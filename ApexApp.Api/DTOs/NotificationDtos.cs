using System.Text.Json;

namespace ApexApp.Api.DTOs;

/// <summary>In-app notification returned to the current user.</summary>
public sealed record NotificationDto(
    long Id,
    string Type,
    string Title,
    string Message,
    JsonElement? Data,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Unread notification count for the bell badge.</summary>
public sealed record NotificationUnreadCountDto(int Count);
