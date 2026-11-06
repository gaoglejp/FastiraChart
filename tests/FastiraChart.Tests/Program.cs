using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.ReferenceLines;
using FastiraChart.Series;

namespace FastiraChart.Tests;

/// <summary>
/// Deterministic tests of FastiraChart's public API. Headless: no window is shown, nothing is rendered to the screen,
/// and no pointer or keyboard input is used.
/// </summary>
internal static class Program
{
    private static readonly Size ChartSize = new(800, 600);

    [STAThread]
    private static int Main()
    {
        (string, Action)[] cases =
        [
            ("Control can be constructed", ControlCanBeConstructed),
            ("Measure and Arrange apply the default template", MeasureAndArrange),
            ("PlotBounds is finite and inside the control", PlotBoundsFinite),
            ("PlotBounds stays finite at degenerate sizes", PlotBoundsFiniteAtDegenerateSizes),
            ("Series collections are per instance", SeriesCollectionsIndependent),
            ("ReferenceLines collections are per instance", ReferenceCollectionsIndependent),
            ("LineSeries Points replace", LineSeriesPointsReplace),
            ("LineSeries Append", LineSeriesAppend),
            ("LineSeries AppendRange", LineSeriesAppendRange),
            ("LineSeries ClearPoints", LineSeriesClearPoints),
            ("Series updates with a chart arranged", SeriesUpdatesWithChartArranged),
            ("Chart dependency property defaults", ChartDefaults),
            ("LineSeries and ChartReferenceLine defaults", ElementDefaults),
            ("Public API matches the expected surface", PublicApiSurface),
            ("Template part names", TemplatePartNames),
            ("XAML namespace maps every public CLR namespace", XamlNamespaceAttributes),
            ("XAML namespace resolves in markup", XamlNamespaceResolution),
            ("Invalid enum values are rejected", InvalidEnumValuesRejected),
            ("Invalid numeric values are rejected", InvalidNumbersRejected),
            ("Explicit OneWay binding is kept by a current-value write", ExplicitOneWayBinding),
            ("Range and lens properties bind TwoWay by default", DefaultTwoWayBinding),
            ("Custom template with missing parts does not crash", CustomTemplateMissingParts),
            ("An inactive lens lets go of its series and reference entries", InactiveLensKeepsNoEntries),
            ("A lens without a vertical scale lets go of its series entries", LensWithoutScaleKeepsNoSeries),
            ("A series removed while the lens is hidden can be collected", SeriesRemovedWhileLensHiddenIsCollected),
        ];

        return TestRunner.Run(cases) == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- construction and layout

    private static void ControlCanBeConstructed()
    {
        var chart = new FastiraChartControl();
        Assert.Equal(0, chart.Series?.Count, "Series.Count");
        Assert.Equal(0, chart.ReferenceLines?.Count, "ReferenceLines.Count");
    }

    private static void MeasureAndArrange()
    {
        var chart = Arranged(new FastiraChartControl());
        Assert.True(VisualTreeHelper.GetChildrenCount(chart) == 1, "the default template produced a visual root");
        Assert.Equal(ChartSize, chart.RenderSize, "RenderSize");
    }

    private static void PlotBoundsFinite()
    {
        var chart = Arranged(new FastiraChartControl { MinimumAxisSpace = new Thickness(40, 8, 16, 32) });
        var plot = chart.PlotBounds;
        Assert.True(IsFinite(plot), $"PlotBounds is finite: {plot}");
        Assert.True(plot.Width > 0 && plot.Height > 0, $"PlotBounds has area: {plot}");
        Assert.True(new Rect(ChartSize).Contains(plot), $"PlotBounds {plot} lies inside the control");
        Assert.True(plot.Left >= 40 && plot.Bottom <= ChartSize.Height - 32, $"MinimumAxisSpace is reserved: {plot}");
    }

    private static void PlotBoundsFiniteAtDegenerateSizes()
    {
        foreach (var size in new[] { new Size(0, 0), new Size(1, 1), new Size(10, 600), new Size(800, 3) })
        {
            var chart = new FastiraChartControl { MinimumAxisSpace = new Thickness(40, 8, 16, 32), Padding = new Thickness(4) };
            chart.Measure(size);
            chart.Arrange(new Rect(size));
            var plot = chart.PlotBounds;
            Assert.True(IsFinite(plot) && plot.Width >= 0 && plot.Height >= 0, $"PlotBounds at {size}: {plot}");
        }
    }

    private static void SeriesCollectionsIndependent()
    {
        var a = new FastiraChartControl();
        var b = new FastiraChartControl();
        Assert.NotSame(a.Series, b.Series, "Series of two controls");
        a.Series.Add(new LineSeries());
        Assert.Equal(1, a.Series.Count, "a.Series.Count");
        Assert.Equal(0, b.Series.Count, "b.Series.Count");
    }

    private static void ReferenceCollectionsIndependent()
    {
        var a = new FastiraChartControl();
        var b = new FastiraChartControl();
        Assert.NotSame(a.ReferenceLines, b.ReferenceLines, "ReferenceLines of two controls");
        a.ReferenceLines.Add(new ChartReferenceLine { Axis = ReferenceLineAxis.Y, Value = 1 });
        Assert.Equal(1, a.ReferenceLines.Count, "a.ReferenceLines.Count");
        Assert.Equal(0, b.ReferenceLines.Count, "b.ReferenceLines.Count");
    }

    // ---------------------------------------------------------------- series data

    private static void LineSeriesPointsReplace()
    {
        var first = new[] { new ChartPoint(0, 0), new ChartPoint(1, 1) };
        var second = new[] { new ChartPoint(5, 5) };
        var series = new LineSeries(first);
        Assert.Same(first, series.Points, "Points is the caller's list");

        series.Points = second;
        Assert.SequenceEqual(second, series.Points, "Points after replace");

        series.Points = null!;
        Assert.Equal(0, series.Points.Count, "null reads back as an empty list");
    }

    private static void LineSeriesAppend()
    {
        var caller = new[] { new ChartPoint(0, 0), new ChartPoint(1, 1) };
        var series = new LineSeries(caller);

        series.Append(new ChartPoint(2, 4));
        series.Append(new ChartPoint(3, double.NaN));

        Assert.SequenceEqual(
            [new ChartPoint(0, 0), new ChartPoint(1, 1), new ChartPoint(2, 4), new ChartPoint(3, double.NaN)],
            series.Points, "Points after two appends");
        Assert.SequenceEqual([new ChartPoint(0, 0), new ChartPoint(1, 1)], caller, "the caller's list is unchanged");

        var empty = new LineSeries();
        empty.Append(new ChartPoint(7, 8));
        Assert.SequenceEqual([new ChartPoint(7, 8)], empty.Points, "Append to an empty series");
    }

    private static void LineSeriesAppendRange()
    {
        var series = new LineSeries([new ChartPoint(0, 0)]);
        var batch = new List<ChartPoint> { new(1, 1), new(2, 2), new(3, 3) };

        series.AppendRange(batch);
        batch.Clear();
        Assert.SequenceEqual([new ChartPoint(0, 0), new ChartPoint(1, 1), new ChartPoint(2, 2), new ChartPoint(3, 3)],
            series.Points, "Points after AppendRange; the batch is copied, not kept");

        series.AppendRange(Array.Empty<ChartPoint>());
        Assert.Equal(4, series.Points.Count, "an empty batch changes nothing");

        series.AppendRange(series.Points);
        Assert.Equal(8, series.Points.Count, "a series' own Points may be appended");

        Assert.Throws<ArgumentNullException>(() => series.AppendRange(null!), "AppendRange(null)");
    }

    private static void LineSeriesClearPoints()
    {
        var caller = new[] { new ChartPoint(0, 0), new ChartPoint(1, 1) };
        var series = new LineSeries(caller) { Stroke = Brushes.Red, StrokeThickness = 3 };
        series.Append(new ChartPoint(2, 2));

        series.ClearPoints();
        Assert.Equal(0, series.Points.Count, "Points after ClearPoints");
        Assert.Same(Brushes.Red, series.Stroke, "Stroke is kept");
        Assert.Equal(3d, series.StrokeThickness, "StrokeThickness is kept");
        Assert.Equal(2, caller.Length, "the caller's list is unchanged");

        series.Append(new ChartPoint(9, 9));
        Assert.SequenceEqual([new ChartPoint(9, 9)], series.Points, "Append after ClearPoints");

        new LineSeries().ClearPoints();
    }

    private static void SeriesUpdatesWithChartArranged()
    {
        var series = new LineSeries();
        var chart = new FastiraChartControl();
        chart.Series.Add(series);
        Arranged(chart);

        series.Points = [new ChartPoint(0, 0), new ChartPoint(50, 50)];
        series.Append(new ChartPoint(100, 100));
        series.AppendRange([new ChartPoint(double.PositiveInfinity, 1), new ChartPoint(120, -20)]);
        series.ClearPoints();
        chart.Series.Remove(series);
        Arranged(chart);

        Assert.True(IsFinite(chart.PlotBounds), "PlotBounds after series updates");
    }

    // ---------------------------------------------------------------- defaults

    private static void ChartDefaults()
    {
        var c = new FastiraChartControl();
        Assert.Equal(0d, c.MinimumX, nameof(c.MinimumX));
        Assert.Equal(100d, c.MaximumX, nameof(c.MaximumX));
        Assert.Equal(0d, c.MinimumY, nameof(c.MinimumY));
        Assert.Equal(100d, c.MaximumY, nameof(c.MaximumY));
        Assert.Equal(6, c.XAxisTargetTickCount, nameof(c.XAxisTargetTickCount));
        Assert.Equal(6, c.YAxisTargetTickCount, nameof(c.YAxisTargetTickCount));
        Assert.Equal(HorizontalAxisPosition.Bottom, c.XAxisPosition, nameof(c.XAxisPosition));
        Assert.Equal(VerticalAxisPosition.Left, c.YAxisPosition, nameof(c.YAxisPosition));
        Assert.Equal(true, c.IsXAxisVisible, nameof(c.IsXAxisVisible));
        Assert.Equal(true, c.IsYAxisVisible, nameof(c.IsYAxisVisible));
        Assert.Equal(LabelRotation.Degree0, c.XAxisLabelRotation, nameof(c.XAxisLabelRotation));
        Assert.Equal(LabelRotation.Degree0, c.YAxisLabelRotation, nameof(c.YAxisLabelRotation));
        Assert.Equal(LabelAnchor.Center, c.XAxisLabelAnchor, nameof(c.XAxisLabelAnchor));
        Assert.Equal(LabelAnchor.Center, c.YAxisLabelAnchor, nameof(c.YAxisLabelAnchor));
        Assert.Equal(4d, c.LabelGap, nameof(c.LabelGap));
        Assert.Equal(new Thickness(0), c.MinimumAxisSpace, nameof(c.MinimumAxisSpace));
        Assert.Equal(true, c.IsHorizontalMajorGridVisible, nameof(c.IsHorizontalMajorGridVisible));
        Assert.Equal(true, c.IsVerticalMajorGridVisible, nameof(c.IsVerticalMajorGridVisible));
        Assert.Equal(1d, c.MajorGridThickness, nameof(c.MajorGridThickness));
        Assert.Equal(true, c.IsHorizontalOriginLineVisible, nameof(c.IsHorizontalOriginLineVisible));
        Assert.Equal(true, c.IsVerticalOriginLineVisible, nameof(c.IsVerticalOriginLineVisible));
        Assert.Equal(1.5d, c.OriginLineThickness, nameof(c.OriginLineThickness));
        Assert.Equal(0d, c.MinimumPlotWidth, nameof(c.MinimumPlotWidth));
        Assert.Equal(0d, c.MinimumPlotHeight, nameof(c.MinimumPlotHeight));
        Assert.Equal(0.8d, c.MinimumPlotRatio, nameof(c.MinimumPlotRatio));
        Assert.Equal(true, c.ClipToPlotArea, nameof(c.ClipToPlotArea));
        Assert.Equal(false, c.IsLensVisible, nameof(c.IsLensVisible));
        Assert.Equal(40d, c.LensMinimumX, nameof(c.LensMinimumX));
        Assert.Equal(60d, c.LensMaximumX, nameof(c.LensMaximumX));
        Assert.Equal(200d, c.LensWidth, nameof(c.LensWidth));
        Assert.Equal(0.5d, c.LensHorizontalPosition, nameof(c.LensHorizontalPosition));
        Assert.Equal(false, c.IsZoomEnabled, nameof(c.IsZoomEnabled));
        Assert.Equal(false, c.IsPanEnabled, nameof(c.IsPanEnabled));
        Assert.Equal(false, c.IsLensInteractionEnabled, nameof(c.IsLensInteractionEnabled));
        Assert.Equal(false, c.IsCrosshairVisible, nameof(c.IsCrosshairVisible));
        Assert.Equal(false, c.IsTooltipVisible, nameof(c.IsTooltipVisible));
        Assert.True(c.MajorGridBrush is not null, "MajorGridBrush has a default");
        Assert.True(c.OriginLineBrush is not null, "OriginLineBrush has a default");
        Assert.True(c.ReferenceLineBrush is not null, "ReferenceLineBrush has a default");
        Assert.True(c.SeriesStroke is not null, "SeriesStroke has a default");
    }

    private static void ElementDefaults()
    {
        var series = new LineSeries();
        Assert.Equal(0, series.Points.Count, "LineSeries.Points");
        Assert.Equal(true, series.IsVisible, "LineSeries.IsVisible");
        Assert.True(series.Stroke is null, "LineSeries.Stroke is null (uses SeriesStroke)");
        Assert.Equal(2d, series.StrokeThickness, "LineSeries.StrokeThickness");

        var line = new ChartReferenceLine();
        Assert.Equal(ReferenceLineAxis.X, line.Axis, "ChartReferenceLine.Axis");
        Assert.Equal(0d, line.Value, "ChartReferenceLine.Value");
        Assert.Equal(true, line.IsVisible, "ChartReferenceLine.IsVisible");
        Assert.True(line.Stroke is null, "ChartReferenceLine.Stroke is null (uses ReferenceLineBrush)");
        Assert.Equal(1d, line.StrokeThickness, "ChartReferenceLine.StrokeThickness");
    }

    // ---------------------------------------------------------------- public surface

    private static void PublicApiSurface()
    {
        var actual = PublicApi(typeof(FastiraChartControl).Assembly);
        var expected = ExpectedApi.Entries.Order(StringComparer.Ordinal).ToList();
        var added = actual.Except(expected, StringComparer.Ordinal).ToList();
        var removed = expected.Except(actual, StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0 && removed.Count == 0,
            $"unexpected public API change; added [{string.Join(", ", added)}], removed [{string.Join(", ", removed)}]");
        Assert.Equal(expected.Count, actual.Count, "public API entry count");
    }

    private static void TemplatePartNames()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(FastiraChartControl.GridHostPartName)] = "PART_GridHost",
            [nameof(FastiraChartControl.ReferenceLineHostPartName)] = "PART_ReferenceLineHost",
            [nameof(FastiraChartControl.SeriesHostPartName)] = "PART_SeriesHost",
            [nameof(FastiraChartControl.ContentHostPartName)] = "PART_ContentHost",
            [nameof(FastiraChartControl.LensHostPartName)] = "PART_LensHost",
            [nameof(FastiraChartControl.InteractionHostPartName)] = "PART_InteractionHost",
            [nameof(FastiraChartControl.AxisHostPartName)] = "PART_AxisHost",
        };

        foreach (var (constant, value) in expected)
        {
            var field = typeof(FastiraChartControl).GetField(constant, BindingFlags.Public | BindingFlags.Static)!;
            Assert.Equal(value, (string?)field.GetRawConstantValue(), constant);
        }

        var declared = typeof(FastiraChartControl).GetCustomAttributes<TemplatePartAttribute>().Select(p => p.Name).Order();
        Assert.SequenceEqual(expected.Values.Order(), declared, "TemplatePart attributes");
    }

    private static void XamlNamespaceAttributes()
    {
        var assembly = typeof(FastiraChartControl).Assembly;
        var mapped = assembly.GetCustomAttributes<XmlnsDefinitionAttribute>()
            .Where(a => a.XmlNamespace == "urn:fastirachart:wpf")
            .Select(a => a.ClrNamespace)
            .Order(StringComparer.Ordinal);
        var exported = assembly.GetExportedTypes()
            .Where(t => t.Namespace != "XamlGeneratedNamespace")
            .Select(t => t.Namespace!)
            .Distinct()
            .Order(StringComparer.Ordinal);
        Assert.SequenceEqual(exported, mapped, "CLR namespaces mapped to urn:fastirachart:wpf");

        var prefix = assembly.GetCustomAttributes<XmlnsPrefixAttribute>().Single(a => a.XmlNamespace == "urn:fastirachart:wpf");
        Assert.Equal("fc", prefix.Prefix, "XmlnsPrefix");
    }

    private static void XamlNamespaceResolution()
    {
        const string xaml = """
            <fc:FastiraChartControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:fc="urn:fastirachart:wpf"
                                    MinimumX="-10" MaximumX="10" MinimumY="-1" MaximumY="1"
                                    XAxisPosition="Top" YAxisPosition="Right"
                                    XAxisLabelRotation="Degree90" XAxisLabelAnchor="End"
                                    MinimumAxisSpace="40,0,0,32">
                <fc:FastiraChartControl.Series>
                    <fc:LineSeries Stroke="SteelBlue" StrokeThickness="1.5" />
                </fc:FastiraChartControl.Series>
                <fc:FastiraChartControl.ReferenceLines>
                    <fc:ChartReferenceLine Axis="Y" Value="0.5" Stroke="Orange" StrokeThickness="2" />
                </fc:FastiraChartControl.ReferenceLines>
            </fc:FastiraChartControl>
            """;

        var chart = (FastiraChartControl)XamlReader.Parse(xaml);
        Assert.Equal(-10d, chart.MinimumX, "MinimumX");
        Assert.Equal(HorizontalAxisPosition.Top, chart.XAxisPosition, "XAxisPosition");
        Assert.Equal(VerticalAxisPosition.Right, chart.YAxisPosition, "YAxisPosition");
        Assert.Equal(LabelRotation.Degree90, chart.XAxisLabelRotation, "XAxisLabelRotation");
        Assert.Equal(LabelAnchor.End, chart.XAxisLabelAnchor, "XAxisLabelAnchor");
        Assert.Equal(new Thickness(40, 0, 0, 32), chart.MinimumAxisSpace, "MinimumAxisSpace");
        Assert.Equal(1.5d, chart.Series.Single().StrokeThickness, "LineSeries.StrokeThickness");
        Assert.Equal(ReferenceLineAxis.Y, chart.ReferenceLines.Single().Axis, "ChartReferenceLine.Axis");
        Assert.Equal(0.5d, chart.ReferenceLines.Single().Value, "ChartReferenceLine.Value");

        var point = (ChartPoint)XamlReader.Parse("""<fc:ChartPoint xmlns:fc="urn:fastirachart:wpf" X="1" Y="2" />""");
        Assert.Equal(new ChartPoint(1, 2), point, "ChartPoint from XAML");

        Arranged(chart);
        Assert.True(IsFinite(chart.PlotBounds), "PlotBounds of the parsed chart");
    }

    // ---------------------------------------------------------------- validation

    private static void InvalidEnumValuesRejected()
    {
        var chart = new FastiraChartControl();
        Assert.Throws<ArgumentException>(() => chart.XAxisPosition = (HorizontalAxisPosition)99, "XAxisPosition");
        Assert.Throws<ArgumentException>(() => chart.YAxisPosition = (VerticalAxisPosition)99, "YAxisPosition");
        Assert.Throws<ArgumentException>(() => chart.XAxisLabelRotation = (LabelRotation)45, "XAxisLabelRotation");
        Assert.Throws<ArgumentException>(() => chart.YAxisLabelRotation = (LabelRotation)(-1), "YAxisLabelRotation");
        Assert.Throws<ArgumentException>(() => chart.XAxisLabelAnchor = (LabelAnchor)99, "XAxisLabelAnchor");
        Assert.Throws<ArgumentException>(() => chart.YAxisLabelAnchor = (LabelAnchor)99, "YAxisLabelAnchor");
        Assert.Throws<ArgumentException>(() => new ChartReferenceLine().Axis = (ReferenceLineAxis)99, "ChartReferenceLine.Axis");

        Assert.Equal(HorizontalAxisPosition.Bottom, chart.XAxisPosition, "a rejected value leaves the property unchanged");
    }

    private static void InvalidNumbersRejected()
    {
        var chart = new FastiraChartControl();
        Assert.Throws<ArgumentException>(() => chart.MinimumX = double.NaN, "MinimumX = NaN");
        Assert.Throws<ArgumentException>(() => chart.MaximumY = double.PositiveInfinity, "MaximumY = +Infinity");
        Assert.Throws<ArgumentException>(() => chart.LensMinimumX = double.NaN, "LensMinimumX = NaN");
        Assert.Throws<ArgumentException>(() => chart.LensHorizontalPosition = 1.5, "LensHorizontalPosition = 1.5");
        Assert.Throws<ArgumentException>(() => chart.LensWidth = -1, "LensWidth = -1");
        Assert.Throws<ArgumentException>(() => chart.MajorGridThickness = -1, "MajorGridThickness = -1");
        Assert.Throws<ArgumentException>(() => chart.XAxisTargetTickCount = 0, "XAxisTargetTickCount = 0");
        Assert.Throws<ArgumentException>(() => chart.MinimumAxisSpace = new Thickness(-1), "MinimumAxisSpace = -1");
        Assert.Throws<ArgumentException>(() => new LineSeries().StrokeThickness = -1, "LineSeries.StrokeThickness = -1");
        Assert.Throws<ArgumentException>(() => new ChartReferenceLine().Value = double.NaN, "ChartReferenceLine.Value = NaN");
    }

    // ---------------------------------------------------------------- binding

    private static void ExplicitOneWayBinding()
    {
        var source = new RangeSource { Value = 10 };
        var chart = new FastiraChartControl();
        BindingOperations.SetBinding(chart, FastiraChartControl.MinimumXProperty,
            new Binding(nameof(RangeSource.Value)) { Source = source, Mode = BindingMode.OneWay });
        Assert.Equal(10d, chart.MinimumX, "source value reaches the chart");

        // A gesture writes ranges as current values; a OneWay binding must neither push them back nor be removed.
        chart.SetCurrentValue(FastiraChartControl.MinimumXProperty, 25d);
        Assert.Equal(25d, chart.MinimumX, "current value is shown");
        Assert.Equal(10d, source.Value, "a OneWay source is not written");
        Assert.True(BindingOperations.GetBindingExpression(chart, FastiraChartControl.MinimumXProperty) is not null,
            "the binding survives");

        source.Value = 30;
        Assert.Equal(30d, chart.MinimumX, "a later source change still reaches the chart");
    }

    private static void DefaultTwoWayBinding()
    {
        DependencyProperty[] twoWay =
        [
            FastiraChartControl.MinimumXProperty, FastiraChartControl.MaximumXProperty,
            FastiraChartControl.MinimumYProperty, FastiraChartControl.MaximumYProperty,
            FastiraChartControl.LensMinimumXProperty, FastiraChartControl.LensMaximumXProperty,
            FastiraChartControl.LensWidthProperty, FastiraChartControl.LensHorizontalPositionProperty,
        ];

        foreach (var property in twoWay)
        {
            var metadata = (FrameworkPropertyMetadata)property.GetMetadata(typeof(FastiraChartControl));
            Assert.True(metadata.BindsTwoWayByDefault, $"{property.Name} binds two-way by default");
        }

        var source = new RangeSource { Value = 10 };
        var chart = new FastiraChartControl();
        BindingOperations.SetBinding(chart, FastiraChartControl.MaximumXProperty, new Binding(nameof(RangeSource.Value)) { Source = source });
        chart.SetCurrentValue(FastiraChartControl.MaximumXProperty, 80d);
        Assert.Equal(80d, source.Value, "a default binding writes a current value back to the source");
        Assert.True(BindingOperations.GetBindingExpression(chart, FastiraChartControl.MaximumXProperty) is not null,
            "the binding survives");

        var lensSource = new RangeSource { Value = 0.25 };
        BindingOperations.SetBinding(chart, FastiraChartControl.LensHorizontalPositionProperty, new Binding(nameof(RangeSource.Value)) { Source = lensSource });
        chart.SetCurrentValue(FastiraChartControl.LensHorizontalPositionProperty, 0.75);
        Assert.Equal(0.75, lensSource.Value, "LensHorizontalPosition writes back by default");
    }

    // ---------------------------------------------------------------- templates

    private static void CustomTemplateMissingParts()
    {
        string[] templates =
        [
            // No part at all.
            """<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Grid /></ControlTemplate>""",
            // Only the plot content host.
            """<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Grid><Grid x:Name="PART_ContentHost" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" /></Grid></ControlTemplate>""",
            // Every drawing host but the lens and interaction hosts.
            """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid>
                    <Grid x:Name="PART_GridHost" /><Grid x:Name="PART_ReferenceLineHost" /><Grid x:Name="PART_SeriesHost" />
                    <Grid x:Name="PART_ContentHost" /><Grid x:Name="PART_AxisHost" />
                </Grid>
            </ControlTemplate>
            """,
            // A part of the wrong type is treated as missing.
            """<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Grid><Border x:Name="PART_SeriesHost" /></Grid></ControlTemplate>""",
        ];

        foreach (var markup in templates)
        {
            var chart = new FastiraChartControl
            {
                Template = (ControlTemplate)XamlReader.Parse(markup),
                IsLensVisible = true,
                IsCrosshairVisible = true,
                IsTooltipVisible = true,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                IsLensInteractionEnabled = true,
            };
            chart.Series.Add(new LineSeries([new ChartPoint(0, 0), new ChartPoint(100, 100)]));
            chart.ReferenceLines.Add(new ChartReferenceLine { Axis = ReferenceLineAxis.Y, Value = 50 });
            Arranged(chart);

            chart.Series[0].Append(new ChartPoint(120, 50));
            chart.MaximumX = 200;
            chart.XAxisLabelRotation = LabelRotation.Degree90;
            Arranged(chart);

            Assert.True(IsFinite(chart.PlotBounds), $"PlotBounds with a reduced template: {chart.PlotBounds}");
        }
    }

    // ---------------------------------------------------------------- lens memory

    private static void InactiveLensKeepsNoEntries()
    {
        var chart = ChartWithLens();
        Assert.Equal((1, 1), LensEntries(chart), "lens entries while shown");

        chart.IsLensVisible = false;
        chart.UpdateLayout();
        Assert.Equal((0, 0), LensEntries(chart), "lens entries while hidden");

        chart.IsLensVisible = true;
        chart.UpdateLayout();
        Assert.Equal((1, 1), LensEntries(chart), "lens entries when shown again");

        // No source inside the main range: visible, and still inactive.
        chart.LensMinimumX = 200;
        chart.LensMaximumX = 300;
        chart.UpdateLayout();
        Assert.Equal((0, 0), LensEntries(chart), "lens entries with its source outside the main range");
    }

    private static void LensWithoutScaleKeepsNoSeries()
    {
        var chart = ChartWithLens();
        Assert.Equal(1, LensEntries(chart).Series, "lens series entries with a scale");

        chart.MaximumY = chart.MinimumY;
        chart.UpdateLayout();
        Assert.Equal(0, LensEntries(chart).Series, "lens series entries without a scale");
    }

    private static void SeriesRemovedWhileLensHiddenIsCollected()
    {
        var chart = ChartWithLens();
        var removed = HideLensAndRemoveSeries(chart);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.True(!removed.IsAlive, "the removed series is still reachable from the chart");
        GC.KeepAlive(chart);
    }

    // Not inlined: no local of the caller may hold the series while it is collected.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference HideLensAndRemoveSeries(FastiraChartControl chart)
    {
        var removed = new WeakReference(chart.Series[0]);

        chart.IsLensVisible = false;
        chart.UpdateLayout();
        chart.Series.Clear();
        chart.UpdateLayout();

        return removed;
    }

    /// <summary>A chart arranged with its lens shown, one series and one reference line.</summary>
    private static FastiraChartControl ChartWithLens()
    {
        var chart = new FastiraChartControl { IsLensVisible = true };
        chart.Series.Add(new LineSeries([new ChartPoint(0, 0), new ChartPoint(100, 100)]));
        chart.ReferenceLines.Add(new ChartReferenceLine { Axis = ReferenceLineAxis.Y, Value = 50 });

        return Arranged(chart);
    }

    /// <summary>The control's internal lens cache count: what the lens holds is not observable through the public API.</summary>
    private static (int References, int Series) LensEntries(FastiraChartControl chart)
    {
        var property = typeof(FastiraChartControl).GetProperty("LensCacheEntries", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var (references, series, _) = ((int, int, int))property.GetValue(chart)!;

        return (references, series);
    }

    // ---------------------------------------------------------------- helpers

    private static FastiraChartControl Arranged(FastiraChartControl chart)
    {
        // An element created in code is initialized when it joins a tree, or by EndInit as here; only then does it
        // take its default (theme) style and template, as it would in a window.
        if (!chart.IsInitialized)
        {
            chart.BeginInit();
            chart.EndInit();
        }

        chart.Measure(ChartSize);
        chart.Arrange(new Rect(ChartSize));
        chart.UpdateLayout();
        return chart;
    }

    private static bool IsFinite(Rect r) =>
        !r.IsEmpty && double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height);

    /// <summary>Every exported type and each public or protected member it declares: <c>Type</c> or <c>Type.Member</c>.</summary>
    private static List<string> PublicApi(Assembly assembly)
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in assembly.GetExportedTypes().Where(t => t.Namespace != "XamlGeneratedNamespace"))
        {
            var name = type.FullName!;
            lines.Add(name);

            foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var visible = m switch
                {
                    MethodBase mb => !mb.IsSpecialName && (mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly),
                    FieldInfo f => !f.IsSpecialName && (f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly),
                    PropertyInfo p => (p.GetMethod ?? p.SetMethod) is { } a && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
                    EventInfo e => e.AddMethod is { } a && (a.IsPublic || a.IsFamily),
                    Type nested => nested.IsNestedPublic || nested.IsNestedFamily,
                    _ => false,
                };

                if (visible)
                {
                    lines.Add(name + "." + m.Name);
                }
            }
        }

        return [.. lines];
    }

    /// <summary>A binding source with one property.</summary>
    private sealed class RangeSource : INotifyPropertyChanged
    {
        private double _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}
