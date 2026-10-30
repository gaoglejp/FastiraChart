using System.Windows;
using System.Windows.Controls;

namespace FastiraChart.Sample.Pages;

/// <summary>Horizontal and vertical reference lines, moved and shown from code.</summary>
public partial class ReferenceLinesPage : UserControl
{
    public ReferenceLinesPage()
    {
        InitializeComponent();

        Wave.Points = SampleData.Sine(count: 300, xMaximum: 100, amplitude: 1, cycles: 2.5);
        UpdateLabels();
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // A change to a line redraws the reference layer; nothing else in the chart is rebuilt.
        if (Threshold is null)
        {
            return;
        }

        Threshold.Value = e.NewValue;
        UpdateLabels();
    }

    private void OnMarkerChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Marker is null)
        {
            return;
        }

        Marker.Value = e.NewValue;
        UpdateLabels();
    }

    private void OnLowerChecked(object sender, RoutedEventArgs e)
    {
        if (LowerLimit is not null && sender is CheckBox box)
        {
            LowerLimit.IsVisible = box.IsChecked == true;
        }
    }

    private void UpdateLabels()
    {
        if (ThresholdText is null || MarkerText is null)
        {
            return;
        }

        ThresholdText.Text = $"Threshold (Y): {Threshold.Value:0.00}";
        MarkerText.Text = $"Marker (X): {Marker.Value:0}";
    }
}
