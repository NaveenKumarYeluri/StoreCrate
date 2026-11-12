# Third-Party Notices

StoreCrate is MIT licensed (see [LICENSE](LICENSE)). It depends on the
following third-party packages and services. This file records their licenses
and attribution. It is provided for convenience and is not legal advice.

## NuGet dependencies

| Package | Version | License | Author / project |
|---|---|---|---|
| Avalonia | 11.2.5 | MIT | AvaloniaUI — https://avaloniaui.net/ |
| Avalonia.Desktop | 11.2.5 | MIT | AvaloniaUI |
| Avalonia.Fonts.Inter | 11.2.5 | MIT | AvaloniaUI (Inter font: SIL Open Font License 1.1) |
| Avalonia.Diagnostics | 11.2.5 | MIT | AvaloniaUI |
| FluentAvaloniaUI | 2.4.1 | MIT | amwx — https://github.com/amwx/FluentAvalonia |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | .NET Foundation — https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | MIT | .NET Foundation |
| Microsoft.Extensions.Http | 8.0.1 | MIT | .NET Foundation |
| Microsoft.Extensions.Logging | 8.0.1 | MIT | .NET Foundation |
| Microsoft.Extensions.Logging.Abstractions | 8.0.1 | MIT | .NET Foundation |
| Microsoft.Extensions.Logging.Console | 8.0.1 | MIT | .NET Foundation |
| Newtonsoft.Json | 13.0.3 | MIT | James Newton-King — https://www.newtonsoft.com/json |
| StoreLib | 1.2.1 | MPL-2.0 | TitleOS — https://github.com/TitleOS/StoreLib |
| xunit | 2.5.3 | Apache-2.0 | .NET Foundation — https://xunit.net/ |
| xunit.runner.visualstudio | 2.5.3 | Apache-2.0 | .NET Foundation |
| Microsoft.NET.Test.Sdk | 17.8.0 | MIT | .NET Foundation |
| coverlet.collector | 6.0.0 | MIT | Coverlet — https://github.com/coverlet-coverage/coverlet |

## StoreLib (MPL-2.0)

StoreCrate references the **unmodified** `StoreLib` NuGet package to perform
FE3 SOAP requests against Microsoft's delivery service. StoreLib is licensed
under the Mozilla Public License, version 2.0.

- Source: https://github.com/TitleOS/StoreLib
- License: https://www.mozilla.org/en-US/MPL/2.0/
- Copyright: TitleOS and StoreLib contributors

Because StoreCrate consumes StoreLib as a binary NuGet dependency and does not
modify its source, StoreLib's MPL-2.0 obligations apply to StoreLib itself, not
to StoreCrate's own source files. The full MPL-2.0 text is available at the
link above. If you redistribute StoreCrate binaries, keep this notice and the
StoreLib license text with the distribution.

## Microsoft services and trademarks

StoreCrate queries public Microsoft Store catalog and delivery endpoints and
displays imagery and metadata provided by those services. Microsoft, Windows,
Microsoft Store, and related marks are trademarks of Microsoft Corporation.
StoreCrate is an independent project and is not affiliated with, endorsed by,
or supported by Microsoft.

Use of these services is subject to Microsoft's applicable terms. StoreCrate
only installs free applications and does not bypass licensing, DRM, or purchase
requirements.

## Fonts and icons

- **Inter** is bundled through `Avalonia.Fonts.Inter` and is licensed under the
  SIL Open Font License 1.1.
- **Segoe UI Variable**, **Segoe UI**, and **Segoe Fluent Icons** are system
  fonts supplied by Windows and are not redistributed by StoreCrate.
- The **StoreCrate icon** (`assets/icon.svg`, `assets/icon.png`,
  `src/StoreCrate.App/Assets/StoreCrate.ico`) is an original design created for
  this project and is MIT licensed with the rest of the source.
