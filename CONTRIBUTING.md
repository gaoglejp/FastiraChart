# Contributing to FastiraChart

Thank you for your interest in FastiraChart. Bug reports, questions and pull requests are welcome.

## Requirements

- **Windows.** FastiraChart is a WPF library; it builds and runs on Windows only.
- **.NET 10 SDK** (any 10.0 feature band; see `global.json`).
- Any editor. Visual Studio 2026, Rider and VS Code with the C# extension all work with the solution.

## Build

From the repository root:

```bash
dotnet restore
dotnet build -c Debug
dotnet build -c Release
```

Both configurations are expected to build with **0 warnings and 0 errors**.

## Test

The tests are a small console program that uses only the public API and needs no test framework package:

```bash
dotnet run --project tests/FastiraChart.Tests -c Release
```

It prints one `PASS` or `FAIL` line per case and exits with code `0` only when every case passes.

## Sample

```bash
dotnet run --project samples/FastiraChart.Sample
```

## Pull requests

- Keep a pull request focused on one change, and describe what it changes and why.
- Build Debug and Release with no new warnings, and run the tests.
- Add or update a test when you change behaviour that the public API exposes.
- Keep public XML documentation accurate for every public member you add or change.
- Note user-visible changes in `CHANGELOG.md` under the next version.
- Discuss a public API change in an issue first: while the version is `0.x` the API may change, but every change
  should be deliberate.

## Dependencies

The library has **no external runtime dependencies** and uses only Windows and .NET built-in functionality. The
default build of the repository needs no NuGet package at all. Please do not add a `PackageReference` to the library,
the sample or the tests; if you believe one is needed, open an issue first.

## Reporting security issues

Please do not report security issues in public issues. See [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
