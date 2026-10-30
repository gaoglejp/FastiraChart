using System.Windows.Controls;
using FastiraChart.Axes;

namespace FastiraChart.Sample.Pages;

/// <summary>Axis placement, label rotation and anchor, tick density, axis space, grid and origin lines.</summary>
public partial class AxesGridPage : UserControl
{
    public AxesGridPage()
    {
        InitializeComponent();

        XPosition.ItemsSource = Enum.GetValues<HorizontalAxisPosition>();
        YPosition.ItemsSource = Enum.GetValues<VerticalAxisPosition>();
        XRotation.ItemsSource = Enum.GetValues<LabelRotation>();
        YRotation.ItemsSource = Enum.GetValues<LabelRotation>();
        XAnchor.ItemsSource = Enum.GetValues<LabelAnchor>();
        YAnchor.ItemsSource = Enum.GetValues<LabelAnchor>();

        Wave.Points = SampleData.Sine(count: 500, xMaximum: 150, amplitude: 1, cycles: 4);
    }
}
