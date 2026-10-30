using FastiraChart.Series;

namespace FastiraChart.Sample;

/// <summary>Deterministic data for the sample pages.</summary>
internal static class SampleData
{
    /// <summary>A sine wave over <c>0..xMaximum</c>, sampled at <paramref name="count"/> points.</summary>
    public static ChartPoint[] Sine(int count, double xMaximum, double amplitude, double cycles, double phase = 0)
    {
        var points = new ChartPoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = xMaximum * i / (count - 1);
            points[i] = new ChartPoint(x, amplitude * Math.Sin(2 * Math.PI * cycles * x / xMaximum + phase));
        }

        return points;
    }

    /// <summary>A noisy signal: a slow sine with a faster ripple and seeded noise, over <c>0..xMaximum</c>.</summary>
    public static ChartPoint[] Signal(int count, double xMaximum, int seed = 1)
    {
        var random = new Random(seed);
        var points = new ChartPoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = xMaximum * i / (count - 1);
            var y = Math.Sin(x / 12) + 0.25 * Math.Sin(x * 1.7) + 0.08 * (random.NextDouble() - 0.5);
            points[i] = new ChartPoint(x, y);
        }

        return points;
    }
}
