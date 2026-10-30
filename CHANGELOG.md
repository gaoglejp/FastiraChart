# Changelog

All notable changes to FastiraChart are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/). While the version is `0.x`, the public API may still change between
minor versions.

## [0.1.0] - Unreleased

First public preview.

### Added

- `FastiraChartControl`, a WPF chart control for .NET 10 on Windows, with one XAML namespace for every public type:
  `xmlns:fc="urn:fastirachart:wpf"`.
- Linear X and Y axes: ranges (`MinimumX`, `MaximumX`, `MinimumY`, `MaximumY`), target tick counts, bottom/top and
  left/right placement, visibility, label rotation (0°, 90°, 180°, 270°), label anchor, label gap and a minimum axis
  space.
- Layout: padding, minimum plot width, height and ratio, and the resulting `PlotBounds`.
- Major grid, origin lines and caller-defined reference lines (`ChartReferenceLine`, X or Y).
- `LineSeries` with `Points` (replace), `Append`, `AppendRange` and `ClearPoints`; non-finite points are drawn as gaps;
  optional clipping to the plot area.
- Lens: a magnified view of an X range drawn inside the plot.
- Interaction, each off by default: wheel zoom, drag pan, lens drag and lens zoom, crosshair and tooltip.
- Custom control templates with seven optional template parts.
- XML documentation for every public type and member.
