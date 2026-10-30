using System.Windows.Controls;

namespace FastiraChart.Sample.Pages;

/// <summary>A chart with two line series and fixed axis ranges.</summary>
public partial class BasicPage : UserControl
{
    public BasicPage()
    {
        InitializeComponent();

        // Setting Points replaces the series' data with a snapshot; the chart draws it on its next frame.
        Wave.Points = SampleData.Sine(count: 400, xMaximum: 100, amplitude: 1, cycles: 3);
        Slow.Points = SampleData.Sine(count: 50, xMaximum: 100, amplitude: 0.6, cycles: 1, phase: Math.PI / 4);
    }
}
