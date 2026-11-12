# Contributing to StoreCrate

Thanks for your interest. StoreCrate is a small project; focused issues and
pull requests are appreciated.

## Before you start

- Search existing issues and pull requests to avoid duplicates.
- For larger changes, open an issue first so the approach can be discussed.
- Do not include downloaded Store packages, product keys, tokens, or personal
  account data in issues or commits.

## Development setup

Requirements:

- Windows 10 version 2004+ or Windows 11
- .NET 8 SDK

```powershell
git clone https://github.com/NaveenKumarYeluri/StoreCrate.git
cd StoreCrate
dotnet restore
dotnet build -c Debug
dotnet test
dotnet run --project src/StoreCrate.App
```

The CLI is the quickest way to exercise the network pipeline:

```powershell
dotnet run --project src/StoreCrate.Cli -- search spotify
dotnet run --project src/StoreCrate.Cli -- packages 9NCBCSZSJRSB
```

## Project conventions

- C# with nullable reference types and implicit usings enabled.
- MVVM via `CommunityToolkit.Mvvm`; keep view models free of UI control types.
- Avalonia XAML uses compiled bindings where `x:DataType` is declared. If you
  add a binding to a new type, add `x:DataType` to the template.
- Prefer `sealed` classes and records for models.
- Keep `StoreCrate.Core` free of Windows-only APIs so it stays unit-testable.
- Put anything that needs `PackageManager`, PowerShell, or Windows APIs in
  `StoreCrate.Windows`.
- Do not add trimming-hostile reflection without a trimmer descriptor.

## Tests

- Add tests for parsers, resolvers, and model logic in
  `tests/StoreCrate.Core.Tests`.
- Run `dotnet test` before opening a pull request.
- Tests must not depend on live Microsoft endpoints. Use captured fixtures.

## Style

- Match the existing formatting; `.editorconfig` and `.gitattributes` are
  checked in.
- Keep UI text short and consistent with the existing tone.
- Do not commit build output (`bin/`, `obj/`, `publish/`).

## Pull requests

A good pull request:

1. explains the problem and the chosen fix;
2. keeps the change scoped;
3. builds and passes tests;
4. includes screenshots for UI changes when possible.

By contributing, you agree that your contributions are licensed under the MIT
License used by this project.

## Code of conduct

Be respectful and constructive. Harassment, personal attacks, and piracy
requests are not welcome. StoreCrate does not support license circumvention,
and such changes will be rejected.
