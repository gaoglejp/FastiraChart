using System.Windows.Controls;

namespace FastiraChart.Sample.Pages;

/// <summary>The lens over a dense signal, with its source range, width and position bound to sliders.</summary>
public partial class LensPage : UserControl
{
    public LensPage()
    {
        InitializeComponent();

        Signal.Points = SampleData.Signal(count: 4000, xMaximum: 200);
    }
}
