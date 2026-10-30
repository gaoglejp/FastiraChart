using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FastiraChart.Sample.Pages;

/// <summary>Zoom, pan, lens gestures, crosshair and tooltip, each switched on and off.</summary>
public partial class InteractionPage : UserControl
{
    private static readonly DependencyPropertyDescriptor[] Ranges =
    [
        DependencyPropertyDescriptor.FromProperty(FastiraChartControl.MinimumXProperty, typeof(FastiraChartControl)),
        DependencyPropertyDescriptor.FromProperty(FastiraChartControl.MaximumXProperty, typeof(FastiraChartControl)),
        DependencyPropertyDescriptor.FromProperty(FastiraChartControl.MinimumYProperty, typeof(FastiraChartControl)),
        DependencyPropertyDescriptor.FromProperty(FastiraChartControl.MaximumYProperty, typeof(FastiraChartControl)),
    ];

    public InteractionPage()
    {
        InitializeComponent();

        Signal.Points = SampleData.Signal(count: 2000, xMaximum: 200, seed: 3);

        // Watch the ranges the gestures write while the page is shown, to display them beside the chart.
        Loaded += (_, _) =>
        {
            foreach (var range in Ranges)
            {
                range.AddValueChanged(Chart, OnRangeChanged);
            }

            UpdateRangeText();
        };
        Unloaded += (_, _) =>
        {
            foreach (var range in Ranges)
            {
                range.RemoveValueChanged(Chart, OnRangeChanged);
            }
        };
    }

    private void OnRangeChanged(object? sender, EventArgs e) => UpdateRangeText();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        Chart.MinimumX = 0;
        Chart.MaximumX = 200;
        Chart.MinimumY = -1.6;
        Chart.MaximumY = 1.6;
        Chart.LensMinimumX = 40;
        Chart.LensMaximumX = 60;
        Chart.LensHorizontalPosition = 0.8;
    }

    private void UpdateRangeText() =>
        RangeText.Text = $"X: {Chart.MinimumX:0.###} .. {Chart.MaximumX:0.###}\nY: {Chart.MinimumY:0.###} .. {Chart.MaximumY:0.###}";
}
