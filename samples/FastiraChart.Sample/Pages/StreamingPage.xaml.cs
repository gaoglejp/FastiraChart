using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FastiraChart.Series;

namespace FastiraChart.Sample.Pages;

/// <summary>The four ways to change a series' points, driven by a timer and buttons.</summary>
public partial class StreamingPage : UserControl
{
    private const double VisibleSpan = 200;
    private const int TrimAbove = 10_000;
    private const int TrimTo = 2_000;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Random _random = new(7);
    private double _nextX;

    public StreamingPage()
    {
        InitializeComponent();

        _timer.Tick += OnTick;

        // Stop the timer when the page leaves the screen (another tab, or the window closing).
        Unloaded += (_, _) => Stop();

        UpdateCount();
    }

    private void OnStartStop(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            Stop();
        }
        else
        {
            _timer.Start();
            StartStopButton.Content = "Stop Append (timer)";
        }
    }

    private void Stop()
    {
        _timer.Stop();
        StartStopButton.Content = "Start Append (timer)";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // Append: one point per tick. All changes are made on the UI thread.
        Stream.Append(NextPoint());

        // A series retains every point; trim a long-running stream by replacing its points.
        if (Stream.Points.Count > TrimAbove)
        {
            Stream.Points = Stream.Points.Skip(Stream.Points.Count - TrimTo).ToArray();
        }

        Follow();
        UpdateCount();
    }

    private void OnAppendRange(object sender, RoutedEventArgs e)
    {
        // AppendRange: a batch is added in one update and drawn in one frame.
        var batch = new ChartPoint[100];
        for (var i = 0; i < batch.Length; i++)
        {
            batch[i] = NextPoint();
        }

        Stream.AppendRange(batch);
        Follow();
        UpdateCount();
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        // Replace: setting Points swaps in a new snapshot. The list is the caller's and is never modified.
        var points = Stream.Points;
        Stream.Points = points.Skip(Math.Max(0, points.Count - 200)).ToArray();
        UpdateCount();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        // ClearPoints: the series stays in the chart, with its style; only its points go.
        Stream.ClearPoints();
        _nextX = 0;
        Chart.MinimumX = 0;
        Chart.MaximumX = VisibleSpan;
        UpdateCount();
    }

    private ChartPoint NextPoint()
    {
        var x = _nextX;
        _nextX += 0.25;
        var y = Math.Sin(x / 8) + 0.3 * Math.Sin(x * 0.9) + 0.1 * (_random.NextDouble() - 0.5);
        return new ChartPoint(x, y);
    }

    private void Follow()
    {
        // Ranges are never derived from the data: scroll the X range to keep the newest point in view.
        var maximum = Math.Max(VisibleSpan, _nextX);
        Chart.MinimumX = maximum - VisibleSpan;
        Chart.MaximumX = maximum;
    }

    private void UpdateCount() => CountText.Text = $"{Stream.Points.Count:N0} points";
}
