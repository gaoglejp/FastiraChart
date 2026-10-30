using System.Globalization;

namespace FastiraChart.Tests;

/// <summary>A minimal test runner: each case is a method that throws to fail.</summary>
internal static class TestRunner
{
    /// <summary>Runs every case in order, prints one PASS or FAIL line per case, and returns the failure count.</summary>
    public static int Run(IReadOnlyList<(string Name, Action Body)> cases)
    {
        var failed = 0;

        foreach (var (name, body) in cases)
        {
            try
            {
                body();
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL  {name}");
                Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{cases.Count - failed} passed, {failed} failed, {cases.Count} total"));
        return failed;
    }
}

/// <summary>Assertions that throw <see cref="AssertionException"/> on failure.</summary>
internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException(string.Create(CultureInfo.InvariantCulture, $"{what}: expected {expected}, actual {actual}"));
        }
    }

    public static void Same(object? expected, object? actual, string what)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new AssertionException($"{what}: expected the same instance");
        }
    }

    public static void NotSame(object? left, object? right, string what)
    {
        if (ReferenceEquals(left, right))
        {
            throw new AssertionException($"{what}: expected different instances");
        }
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what)
    {
        var e = expected.ToArray();
        var a = actual.ToArray();
        if (!e.SequenceEqual(a))
        {
            throw new AssertionException(string.Create(CultureInfo.InvariantCulture,
                $"{what}: expected [{string.Join(", ", e)}], actual [{string.Join(", ", a)}]"));
        }
    }

    public static TException Throws<TException>(Action action, string what)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"{what}: expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new AssertionException($"{what}: expected {typeof(TException).Name}, nothing was thrown");
    }
}

/// <summary>A failed assertion.</summary>
internal sealed class AssertionException(string message) : Exception(message);
