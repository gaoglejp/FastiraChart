# FastiraChart

A WPF line chart control for .NET 10 on Windows.

> **Status: public preview (0.1.0).** The API may still change before 1.0.

## Overview

FastiraChart is a single WPF control, `FastiraChartControl`, that draws linear X/Y axes, a major grid, origin and
reference lines, line series, a magnifying lens, and optional zoom, pan, crosshair and tooltip interaction. It is
written against WPF only and has no third-party dependencies.

FastiraChart is designed with update performance as a primary goal: each layer of the chart (grid, reference lines,
series, axes, lens, overlay) is redrawn only when something it is drawn from changes, and a series can be appended to
point by point or in batches without rebuilding the rest of the chart.

## Features

- Linear X and Y axes with automatic tick values, axis placement (bottom/top, left/right) and visibility
- Axis labels with rotation (0°, 90°, 180°, 270°), anchor and gap; labels follow the current culture
- A minimum axis space, padding and minimum plot size; the resulting plot rectangle is exposed as `PlotBounds`
- Major grid and origin lines, each switchable per direction
- Reference lines at any X or Y value
- `LineSeries` with replace, append, batch append and clear; non-finite points draw as gaps
- Optional clipping of series to the plot area
- A lens that shows an X range magnified inside the plot
- Wheel zoom, drag pan, lens drag and lens zoom, crosshair and tooltip — all off by default
- Custom `ControlTemplate`s with optional template parts
- XML documentation for every public type and member

## Requirements

- Windows
- .NET 10 (`net10.0-windows`), WPF
- No third-party packages

## Installation

**NuGet package planned for the first public preview.** It has not been published yet; until it is, build from source
(see [Build from Source](#build-from-source)) and reference the project:

```xml
<ProjectReference Include="path\to\FastiraChart\src\FastiraChart\FastiraChart.csproj" />
```

Once the package is published, the package ID will be `FastiraChart`.

## Quick Start

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fc="urn:fastirachart:wpf"
        Title="Chart" Width="800" Height="500">
    <fc:FastiraChartControl x:Name="Chart"
                            MinimumX="0" MaximumX="100"
                            MinimumY="-1" MaximumY="1"
                            XAxisLabelRotation="Degree90"
                            MinimumAxisSpace="40,0,0,32"
                            IsZoomEnabled="True" IsPanEnabled="True">
        <fc:FastiraChartControl.Series>
            <fc:LineSeries x:Name="Signal" Stroke="SteelBlue" StrokeThickness="1.5" />
        </fc:FastiraChartControl.Series>
    </fc:FastiraChartControl>
</Window>
```

```csharp
using FastiraChart.Series;

var points = new ChartPoint[101];
for (var i = 0; i < points.Length; i++)
{
    points[i] = new ChartPoint(i, Math.Sin(i / 10.0));
}

Signal.Points = points;                          // replace all points
Signal.Append(new ChartPoint(101, 0.5));         // add one point
Signal.AppendRange([new(102, 0.4), new(103, 0.3)]); // add a batch in one update
Signal.ClearPoints();                            // remove all points, keep the series
```

The axis ranges are always the ones you set: FastiraChart never derives a range from the data.

## XAML Namespace

Every public type is available under one XAML namespace, whichever CLR namespace it is declared in:

```xml
xmlns:fc="urn:fastirachart:wpf"
```

| CLR namespace | Types |
| --- | --- |
| `FastiraChart` | `FastiraChartControl` |
| `FastiraChart.Axes` | `HorizontalAxisPosition`, `VerticalAxisPosition`, `LabelRotation`, `LabelAnchor` |
| `FastiraChart.ReferenceLines` | `ChartReferenceLine`, `ReferenceLineAxis` |
| `FastiraChart.Series` | `LineSeries`, `ChartPoint` |

## Series

A `LineSeries` joins its points, in the order given, with straight lines. Add series to `FastiraChartControl.Series`;
they are drawn in collection order.

| Member | Description |
| --- | --- |
| `Points` | The points (`IReadOnlyList<ChartPoint>`). Setting it **replaces** all points. The list is a snapshot: later changes to a list already set are not noticed. Default: empty |
| `Append(ChartPoint)` | Adds one point after the last |
| `AppendRange(IReadOnlyList<ChartPoint>)` | Adds a batch after the last, in one update |
| `ClearPoints()` | Removes every point; the series keeps its place and style |
| `Stroke` | Line brush. `null` (default) uses the chart's `SeriesStroke` |
| `StrokeThickness` | Line thickness in DIP. Default `2` |
| `IsVisible` | Whether the series is drawn. Default `true` |

- A point that is not finite (`NaN`, `±Infinity`) is a **gap**: nothing is drawn across it.
- Points outside the axis ranges are kept and placed where they lie; `ClipToPlotArea` (default `true`) clips the
  series to the plot.
- Points are never sorted, merged or thinned.
- FastiraChart never modifies a list, brush or series you give it.

## Streaming Updates

All changes are made on the UI thread, like any WPF object. Each call is one update, and the chart redraws once per
frame however many updates came before it.

```csharp
// One point at a time, for example from a DispatcherTimer.
Signal.Append(new ChartPoint(x, y));

// Many points at once: one update, one redraw.
Signal.AppendRange(batch);

// Scroll the X range to follow the data.
Chart.MinimumX = x - 200;
Chart.MaximumX = x;

// A series keeps every point it is given; trim a long-running stream by replacing its points.
if (Signal.Points.Count > 100_000)
{
    Signal.Points = Signal.Points.Skip(Signal.Points.Count - 20_000).ToArray();
}
```

After an `Append`, `AppendRange` or `ClearPoints`, `Points` reads back the series' own list. Copy it (for example with
`ToArray()`) if you need to keep it across later updates.

## Axes

| Property | Description | Default |
| --- | --- | --- |
| `MinimumX`, `MaximumX` | X range | `0`, `100` |
| `MinimumY`, `MaximumY` | Y range | `0`, `100` |
| `XAxisTargetTickCount`, `YAxisTargetTickCount` | Approximate number of ticks (1–1000) | `6` |
| `XAxisPosition` | `Bottom` or `Top` | `Bottom` |
| `YAxisPosition` | `Left` or `Right` | `Left` |
| `IsXAxisVisible`, `IsYAxisVisible` | Whether the axis is drawn; its space stays reserved | `true` |
| `XAxisLabelRotation`, `YAxisLabelRotation` | `Degree0`, `Degree90`, `Degree180`, `Degree270` | `Degree0` |
| `XAxisLabelAnchor`, `YAxisLabelAnchor` | `Start`, `Center`, `End`: which point along the label's text (in its reading direction) is pinned to the tick | `Center` |
| `LabelGap` | Gap between a tick and its label, in DIP | `4` |
| `MinimumAxisSpace` | Space reserved per edge for ticks and labels (a `Thickness`); labels may need more | `0` |
| `MinimumPlotWidth`, `MinimumPlotHeight`, `MinimumPlotRatio` | Lower bounds on the plot size | `0`, `0`, `0.8` |
| `PlotBounds` | Read-only: the plot rectangle in the control's coordinates | — |

The range and lens properties bind **two-way by default**, so a gesture that changes them updates a bound view model.
Gestures write them as current values (`SetCurrentValue`), so a `OneWay` binding survives a zoom or pan.

Invalid values are rejected by the property system with an `ArgumentException`: non-finite range values, undefined
enum values, negative thicknesses and out-of-range counts.

## Grid and Reference Lines

| Property | Description | Default |
| --- | --- | --- |
| `IsHorizontalMajorGridVisible`, `IsVerticalMajorGridVisible` | Major grid lines at the Y and X ticks | `true` |
| `MajorGridBrush`, `MajorGridThickness` | Grid style | theme brush, `1` |
| `IsHorizontalOriginLineVisible`, `IsVerticalOriginLineVisible` | Lines at y = 0 and x = 0 | `true` |
| `OriginLineBrush`, `OriginLineThickness` | Origin line style | theme brush, `1.5` |
| `ReferenceLines` | The chart's reference lines | empty |
| `ReferenceLineBrush` | Brush for a reference line without a `Stroke` | theme brush |

A `ChartReferenceLine` marks one value:

```xml
<fc:FastiraChartControl.ReferenceLines>
    <fc:ChartReferenceLine Axis="Y" Value="0.8" Stroke="IndianRed" StrokeThickness="1.5" />
    <fc:ChartReferenceLine Axis="X" Value="50" />
</fc:FastiraChartControl.ReferenceLines>
```

| Member | Description | Default |
| --- | --- | --- |
| `Axis` | `ReferenceLineAxis.X` (a vertical line at an X value) or `ReferenceLineAxis.Y` (a horizontal line at a Y value) | `X` |
| `Value` | The value marked (finite) | `0` |
| `Stroke` | Line brush; `null` uses the chart's `ReferenceLineBrush` | `null` |
| `StrokeThickness` | Line thickness in DIP | `1` |
| `IsVisible` | Whether the line is drawn | `true` |

## Lens

The lens draws an X range magnified across part of the plot, together with the grid, reference and origin lines,
series and X ticks inside it. It never moves the plot.

| Property | Description | Default |
| --- | --- | --- |
| `IsLensVisible` | Whether the lens is shown | `false` |
| `LensMinimumX`, `LensMaximumX` | The X range shown in the lens | `40`, `60` |
| `LensWidth` | Lens width in DIP (capped at the plot width) | `200` |
| `LensHorizontalPosition` | Where the lens stands along the plot: `0` left … `1` right | `0.5` |

```xml
<fc:FastiraChartControl IsLensVisible="True"
                        LensMinimumX="90" LensMaximumX="110"
                        LensWidth="280" LensHorizontalPosition="0.5" />
```

## Interaction

Every switch is off by default, so a chart never takes the wheel or the mouse from its host unless asked to.

| Property | Gesture |
| --- | --- |
| `IsZoomEnabled` | Wheel over the plot zooms the X range about the pointer; with Shift, the Y range |
| `IsPanEnabled` | Left-button drag over the plot pans both ranges |
| `IsLensInteractionEnabled` | Drag inside the lens moves it; with zoom on, the wheel inside the lens zooms the range it shows |
| `IsCrosshairVisible` | A crosshair follows the pointer over the plot or the lens |
| `IsTooltipVisible` | A tooltip beside the pointer shows the values under it |

A gesture that is off, or outside the plot, is left unhandled for the parent (a surrounding `ScrollViewer` still
scrolls). The wheel with Ctrl or Alt held is always left to the parent.

## Custom ControlTemplate

The control can be restyled and retemplated. Its template parts are seven optional panels, listed here from bottom to
top. Supply them as **siblings** in one root panel that spans the whole control — not inside the border element, which
would offset them by the border thickness twice. The order of the parts is their drawing order.

| Order | Part name | Constant | Type | Draws |
| --- | --- | --- | --- | --- |
| 1 | `PART_GridHost` | `GridHostPartName` | `Panel` | Major grid |
| 2 | `PART_ReferenceLineHost` | `ReferenceLineHostPartName` | `Panel` | Reference lines, then origin lines |
| 3 | `PART_SeriesHost` | `SeriesHostPartName` | `Panel` | Series, in collection order |
| 4 | `PART_ContentHost` | `ContentHostPartName` | `FrameworkElement` | Nothing: arranged onto the plot, for your own plot-aligned content |
| 5 | `PART_LensHost` | `LensHostPartName` | `Panel` | The lens |
| 6 | `PART_InteractionHost` | `InteractionHostPartName` | `Panel` | Crosshair, then tooltip |
| 7 | `PART_AxisHost` | `AxisHostPartName` | `Panel` | Tick marks, then labels |

Every part is optional. A missing part **does not crash** the control, but the corresponding functionality may not
render: without `PART_LensHost` no lens is drawn, without `PART_InteractionHost` no crosshair or tooltip is drawn (the
gestures still work), and so on. The drawing hosts should not be hit-testable (`IsHitTestVisible="False"`). The border
element may apply `BorderThickness` but must not apply `Padding`: the control applies padding itself.

```xml
<ControlTemplate TargetType="fc:FastiraChartControl">
    <Grid>
        <Border Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}" />
        <Grid x:Name="PART_GridHost" IsHitTestVisible="False" />
        <Grid x:Name="PART_ReferenceLineHost" IsHitTestVisible="False" />
        <Grid x:Name="PART_SeriesHost" IsHitTestVisible="False" />
        <Grid x:Name="PART_ContentHost" />
        <Grid x:Name="PART_LensHost" IsHitTestVisible="False" />
        <Grid x:Name="PART_InteractionHost" IsHitTestVisible="False" />
        <Grid x:Name="PART_AxisHost" IsHitTestVisible="False" />
    </Grid>
</ControlTemplate>
```

## Known Limitations

1. A long-running `LineSeries` retains all points; trim it by replacing `Points` or with `ClearPoints`.
2. Changing a series rebuilds its WPF line geometry; the cost increases with the series' point count.
3. Mixed-DPI behavior has not yet been validated on multiple physical monitors with different DPI.
4. Lens labels near the lens edge may be clipped.
5. Long axis labels may overlap; label rotation can be used to manage this.
6. `ClipToPlotArea="False"` may allow a series to draw beyond the chart control.
7. Label formatting follows `CultureInfo.CurrentCulture` when the relevant axis rebuilds.

## Performance Philosophy

FastiraChart is designed with update performance as a primary goal:

- **Layered redraw.** The chart is drawn as separate layers. A change marks only the layers it affects, and within a
  layer only the part drawn from the axis it reaches; everything else is reused.
- **Drawn per frame, not per change.** Many updates before a frame cost one rebuild, at the frame.
- **Cheap appends.** `Append` and `AppendRange` add to storage the series owns, without copying the points already
  there.
- **No per-element visuals.** Each layer is one drawing element, however many ticks, labels, lines or points it draws.
- **No hidden work.** Ranges are never derived from the data, and points are never sorted or resampled.

## Samples

`samples/FastiraChart.Sample` is a small WPF application with one page per feature area: Basic, Streaming, Axes & Grid,
Reference Lines, Lens and Interaction. It uses only the public API.

```bash
dotnet run --project samples/FastiraChart.Sample
```

## Build from Source

Requires Windows and the .NET 10 SDK.

```bash
git clone https://github.com/gaoglejp/FastiraChart.git
cd FastiraChart
dotnet build -c Release
dotnet run --project tests/FastiraChart.Tests -c Release
```

The library, the sample and the tests build without any NuGet package. To produce a local package:

```bash
dotnet pack src/FastiraChart/FastiraChart.csproj -c Release -o artifacts
```

## Contributing

Issues and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md). Please report security issues privately
as described in [SECURITY.md](SECURITY.md).

## License

FastiraChart is licensed under the [MIT License](LICENSE).

## Roadmap

- Publish the `0.1.0` preview package on NuGet.
- Gather feedback on the public API before a `1.0` release.
- Validate mixed-DPI behavior on multi-monitor hardware.

**Future idea, not implemented and not scheduled:** an optional renderer backend (for example Direct2D or Skia) as a
separate package. Nothing in FastiraChart today depends on it.
