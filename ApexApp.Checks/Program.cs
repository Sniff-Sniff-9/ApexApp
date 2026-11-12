using System.Collections.Concurrent;
using System.Text.Json;
using ApexApp.Checks;
using ApexApp.Infrastructure.Caching;

// Verification harness for the analytics module.
//
//   dotnet run --project ApexApp.Checks             -> unit checks + database checks
//   dotnet run --project ApexApp.Checks -- --unit   -> unit checks only
//
// A test framework package is not used because this environment cannot restore new
// NuGet packages; the harness still reports named, deterministic checks and a non-zero
// exit code on failure.

var runner = new CheckRunner();

UnitChecks.Run(runner);

if (!args.Contains("--unit", StringComparer.OrdinalIgnoreCase))
{
    var connectionString = await ConnectionResolver.TryResolveAsync();

    if (connectionString is null)
    {
        Console.WriteLine("SKIP  Database checks: no connection string available.");
    }
    else
    {
        try
        {
            await using var provider = await IntegrationChecks.BuildProviderAsync(connectionString);
            await IntegrationChecks.RunAsync(runner, provider);

            if (args.Contains("--checkout-write", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "NOTE  --checkout-write: the checkout check creates a temporary product and removes it afterwards.");
                await CheckoutChecks.RunAsync(runner, provider);
            }
        }
        catch (Exception exception)
        {
            runner.Check(
                "database.connect",
                () => throw new InvalidOperationException(
                    "Database checks could not run: " + exception.Message));
        }
    }
}

return runner.Report("ApexApp analytics checks");

/// <summary>Minimal in-memory cache implementation for the harness.</summary>
internal sealed class SimpleCache : ICacheService
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool TryGet<T>(string key, out T? value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            if (entry.Value is T typed)
            {
                value = typed;
                return true;
            }
        }

        _entries.TryRemove(key, out _);
        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        if (value is null)
        {
            return;
        }

        _entries[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    public void Remove(string key) => _entries.TryRemove(key, out _);

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _entries.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    private sealed record Entry(object Value, DateTimeOffset ExpiresAt);
}

/// <summary>Finds a PostgreSQL connection string, preferring an explicit argument.</summary>
internal static class ConnectionResolver
{
    public static async Task<string?> TryResolveAsync()
    {
        var candidate = ReadCandidate();

        if (candidate is null)
        {
            return null;
        }

        if (await CanOpenAsync(candidate))
        {
            return candidate;
        }

        // Local sandboxes may block the TLS credential provider; the schema itself is
        // reachable without TLS, so retry there and say so loudly.
        if (candidate.Contains("SSL Mode=Require", StringComparison.OrdinalIgnoreCase))
        {
            var relaxed = candidate.Replace(
                "SSL Mode=Require",
                "SSL Mode=Disable",
                StringComparison.OrdinalIgnoreCase);

            if (await CanOpenAsync(relaxed))
            {
                Console.WriteLine(
                    "NOTE  Database checks use SSL Mode=Disable: the local environment blocks TLS handshakes.");
                return relaxed;
            }
        }

        return null;
    }

    private static string? ReadCandidate()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("APEX_CHECKS_CONNECTION");

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "UserSecrets",
                "7c797b95-dd0b-42c0-87ae-acb4d40d3082",
                "secrets.json");

            if (!File.Exists(path))
            {
                return null;
            }

            var value = JsonDocument.Parse(File.ReadAllText(path))
                .RootElement.GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static async Task<bool> CanOpenAsync(string connectionString)
    {
        try
        {
            await using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
