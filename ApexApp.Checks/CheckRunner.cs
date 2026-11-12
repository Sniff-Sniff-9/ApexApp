namespace ApexApp.Checks;

/// <summary>
/// Minimal assertion harness: reports PASS/FAIL for each named check and returns a
/// process exit code. A test framework package is intentionally not used because the
/// build environment cannot restore new NuGet packages.
/// </summary>
internal sealed class CheckRunner
{
    private readonly List<(string Name, bool Passed, string? Message)> _results = [];

    public void Check(string name, Func<bool> check)
    {
        try
        {
            var passed = check();
            _results.Add((name, passed, passed ? null : "assertion returned false"));
        }
        catch (Exception exception)
        {
            _results.Add((name, false, exception.GetType().Name + ": " + exception.Message));
        }
    }

    public void Check(string name, Action check)
    {
        try
        {
            check();
            _results.Add((name, true, null));
        }
        catch (Exception exception)
        {
            _results.Add((name, false, exception.GetType().Name + ": " + exception.Message));
        }
    }

    public async Task CheckAsync(string name, Func<Task> check)
    {
        try
        {
            await check();
            _results.Add((name, true, null));
        }
        catch (Exception exception)
        {
            _results.Add((name, false, exception.GetType().Name + ": " + exception.Message));
        }
    }

    public async Task CheckAsync(string name, Func<Task<bool>> check)
    {
        try
        {
            var passed = await check();
            _results.Add((name, passed, passed ? null : "assertion returned false"));
        }
        catch (Exception exception)
        {
            _results.Add((name, false, exception.GetType().Name + ": " + exception.Message));
        }
    }

    public int Report(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 100));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 100));

        foreach (var (name, passed, message) in _results)
        {
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}");

            if (!passed && message is not null)
            {
                Console.WriteLine($"      -> {message}");
            }
        }

        var failed = _results.Count(result => !result.Passed);

        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{_results.Count - failed} passed, {failed} failed, {_results.Count} total");

        return failed == 0 ? 0 : 1;
    }

    public static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
