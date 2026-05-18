# PaymentTerminalService

PaymentTerminalService is a self-hosted Windows Service that exposes a REST API for POS systems to interact with payment terminals.

The service owns terminal communication, selection, safety, status/session persistence, and recovery. POS applications own business workflow and user interaction.

## Overview

| Principle | Description |
| --- | --- |
| Single active terminal | Only one terminal is active at a time. |
| Sequential access | Terminal operations are serialized by the service. |
| Service-owned communication | The service manages terminal I/O, terminal lifetime, and recovery. |
| POS-owned workflow | POS/UI code drives the business flow and polls for status. |
| OpenAPI-first | The API contract is defined in `openapi/PaymentTerminalService.v1.openapi.yaml`. |
| Local REST integration | Production binding is loopback by default: `http://127.0.0.1:7777/`. |

## Technology Stack

| Component | Technology |
| --- | --- |
| Runtime | .NET Framework 4.6.2 |
| Language | C# |
| API hosting | OWIN self-host / HttpListener |
| API protocol | JSON over HTTP |
| Service mode | Windows Service; same executable can run as console app |
| API documentation | OpenAPI / Swagger via Swashbuckle |
| Code generation | NSwag |
| Dependency injection | SimpleInjector |
| Logging | NLog with `System.Diagnostics.Trace` bridge |
| Terminal hardware | Verifone Yomani XR through ECRTerminal SDK |
| Installer | WiX Toolset v3 MSI plus Burn EXE bootstrapper |
| Unit testing | MSTest v2 |

## Solution Structure

```text
PaymentTerminalService/
|-- openapi/
|   `-- PaymentTerminalService.v1.openapi.yaml
|-- nswag/
|   |-- PaymentTerminalServiceClient.nswag
|   |-- PaymentTerminalServiceControllers.nswag
|   `-- PaymentTerminalServiceModel.nswag
|-- src/
|   |-- PaymentTerminalService.Model/
|   |-- PaymentTerminalService.Web/
|   |-- PaymentTerminalService.Host/
|   |-- PaymentTerminalService.Client/
|   |-- PaymentTerminalService.Installer/
|   |-- PaymentTerminalService.Bootstrapper/
|   |-- PaymentTerminalService.TestApp1/
|   `-- tests/PaymentTerminalService.Tests/
|-- CONTRIBUTING.md
|-- readme-nuget.txt
`-- README.md
```

## Projects

| Project | Purpose |
| --- | --- |
| `PaymentTerminalService.Model` | Shared DTOs, interfaces, exceptions, terminal base classes, and session storage helpers. Also packed as `PaymentTerminalService.Model`. |
| `PaymentTerminalService.Web` | Web API controllers and exception-to-HTTP response mapping. |
| `PaymentTerminalService.Host` | Executable Windows Service / console host, OWIN startup, DI registration, terminal catalog, and terminal implementations. |
| `PaymentTerminalService.Client` | POS-facing client library with `PaymentTerminalServiceManager` and status polling. Also packed as `PaymentTerminalService.Client`. |
| `PaymentTerminalService.Installer` | WiX MSI that installs and registers the Windows Service. |
| `PaymentTerminalService.Bootstrapper` | WiX Burn EXE wrapper around the MSI; deployment artifact is `PaymentTerminalService.Setup.x64.exe`. |
| `PaymentTerminalService.TestApp1` | Local WPF utility for manually exercising service workflows. |
| `PaymentTerminalService.Tests` | MSTest coverage for model behavior, utility classes, session storage, and API error mapping. |

## Runtime Architecture

```text
POS application
  - owns business workflow and user interaction
  - calls REST endpoints and polls status
        |
        | HTTP / JSON
        v
PaymentTerminalService.Host
  - OWIN Web API host
  - SimpleInjector dependency container
  - PaymentTerminalManagementService
  - one selected IPaymentTerminal instance
        |
        | Serial / terminal SDK
        v
Payment terminal hardware
```

The host enforces one process instance with the named mutex `Global\PaymentTerminalService.Host`. A second instance exits with code `2`.

## API Contract

The OpenAPI document is the source of truth:

```text
openapi/PaymentTerminalService.v1.openapi.yaml
```

The contract defines terminal discovery, selection, status, session, transaction, abort, loyalty, prompt, and confirm operations. Runtime routes include:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/terminals` | Discover terminals and current selection. |
| `GET` | `/terminals/selected` | Read selected terminal and connection. |
| `PUT` | `/terminals/selected` | Select and activate a terminal. |
| `DELETE` | `/terminals/selected` | Deselect terminal and release resources. |
| `GET` | `/terminals/selected/settings` | Read selected terminal settings. |
| `GET` | `/terminals/selected/status` | Read latest terminal status. |
| `GET` | `/terminals/selected/session` | Read current session status history. |
| `POST` | `/terminals/selected/purchase` | Start purchase. |
| `POST` | `/terminals/selected/refund` | Start refund. |
| `POST` | `/terminals/selected/reversal` | Start reversal. |
| `POST` | `/terminals/selected/abort` | Abort an active transaction. |
| `POST` | `/terminals/selected/loyalty/activate` | Activate loyalty flow. |
| `POST` | `/terminals/selected/loyalty/deactivate` | Deactivate loyalty flow. |
| `POST` | `/terminals/selected/prompt` | Respond to terminal prompt. |
| `POST` | `/terminals/selected/confirm` | Confirm terminal status. |

The raw OpenAPI YAML is served by the host at `GET /apidoc`.

Swagger UI is available at `/swagger/ui/index` in Debug builds. In Release builds, set `PAYMENT_TERMINAL_SERVICE_SWAGGER_ENABLED=1` to enable it.

## Configuration

Configuration comes from `App.config` app settings. Release builds apply `App.Release.config`.

| Key | Debug value | Release value | Description |
| --- | --- | --- | --- |
| `BaseUrl` | `http://127.0.0.1:7575/` | `http://127.0.0.1:7777/` | OWIN listener URL. |
| `LogDirectory` | empty | `C:\ProVersa\PaymentTerminalService\Logs` | Log output directory. Empty falls back to `<BaseDir>\Logs`. |
| `TerminalSessionDirectory` | empty | `C:\ProVersa\PaymentTerminalService\TerminalSessions` | Terminal session storage directory. Empty falls back to `<BaseDir>\TerminalSessions`. |

Terminal catalog and selection state are stored in `terminals.json`. The service creates or merges this file from the built-in catalog definition on startup.

Debug builds include a `TestTerminal-001` catalog entry. Release builds include the Verifone Yomani XR terminal entry.

## Running

### Console Mode

Build and run `PaymentTerminalService.Host.exe` interactively:

```bat
src\PaymentTerminalService.Host\bin\Debug\PaymentTerminalService.Host.exe
```

Press Enter in the console window to stop the host.

### Windows Service Mode

Install the service using the Burn bootstrapper built by `PaymentTerminalService.Bootstrapper`:

```text
PaymentTerminalService.Setup.x64.exe
```

The MSI installs the service under `C:\ProVersa\PaymentTerminalService`, registers it as LocalSystem, configures automatic startup, and stops/removes it during upgrade or uninstall.

## Building

Prerequisites:

- Visual Studio 2022
- .NET Framework 4.6.2 developer pack
- WiX Toolset v3
- WiX Visual Studio extension
- Restored NuGet packages
- Verifone ECRTerminal SDK dependency available for the host project

Restore packages, then build the solution:

```bat
nuget restore src\PaymentTerminalService.sln
msbuild src\PaymentTerminalService.sln /p:Configuration=Release /p:Platform=x64
```

To build the deployable bootstrapper directly:

```bat
msbuild src\PaymentTerminalService.Bootstrapper\PaymentTerminalService.Bootstrapper.wixproj /p:Configuration=Release /p:Platform=x64 /p:BundleVersion=1.0.0
```

## Testing

Run the MSTest assembly after building:

```bat
vstest.console src\tests\PaymentTerminalService.Tests\bin\Release\PaymentTerminalService.Tests.dll
```

Coverage currently includes:

- API model equality and model contracts
- Money conversion helpers
- Terminal status formatting
- Collection and dictionary helpers
- Session file persistence and timestamp file naming
- API exception filter HTTP response mapping

## Code Generation

NSwag configuration lives in `nswag/` and generates code from `openapi/PaymentTerminalService.v1.openapi.yaml`.

| Config | Output |
| --- | --- |
| `PaymentTerminalServiceModel.nswag` | Model DTOs in `PaymentTerminalService.Model`. |
| `PaymentTerminalServiceControllers.nswag` | Generated controller base/contracts in `PaymentTerminalService.Web`. |
| `PaymentTerminalServiceClient.nswag` | Generated client in `PaymentTerminalService.Client`. |

When changing the API, update the OpenAPI file first, regenerate the generated code, then update implementation/tests.

## Versioning

`src\Version.props` is the source of truth for installer, assembly file, informational, and NuGet package versions. `src\Generate-VersionInfo.ps1` generates `src\GeneratedVersionInfo.cs`.

More details are in `src\readme-versioning.txt`.

## NuGet Packages

The solution publishes two internal packages:

| Package | Description |
| --- | --- |
| `PaymentTerminalService.Model` | Shared interfaces, DTOs, exceptions, base abstractions, and helpers. |
| `PaymentTerminalService.Client` | POS client library with high-level operations and status polling. |

See `readme-nuget.txt` for local pack/push examples.

## CI/CD

No active Azure Pipelines YAML files are currently checked in under `azure-pipelines/`. Historical pipeline notes and deleted docs should not be treated as current build instructions.

The intended deployment artifact is the versioned Burn EXE bootstrapper, not the raw MSI.

## Contributing

Follow `CONTRIBUTING.md` for code style, diagnostics, method ordering, and tracing expectations.
