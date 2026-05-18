# PaymentTerminalService.TestApp1

Local WPF development utility for manually exercising the PaymentTerminalService API and terminal workflows.

This project is not part of product deployment and is not included in the MSI or Burn bootstrapper. It is a developer/operator aid for checking discovery, selection, transaction calls, prompt handling, and status/session polling against a running service.

## Configuration

The app reads the service base URL from `App.config`:

| Key | Debug value | Release value |
| --- | --- | --- |
| `PaymentTerminalServiceUrl` | `http://127.0.0.1:7575/` | `http://127.0.0.1:7777/` |

The Release value is applied by `App.Release.config`.

## Capabilities

- Load terminal catalog from `GET /terminals`.
- Select a terminal connection and persist it through the service.
- Read terminal settings, latest status, and session history.
- Start purchase, reversal, and refund operations.
- Abort active transactions, including forced abort.
- Toggle loyalty flow when the selected terminal supports it.
- Respond to terminal prompts and confirmations.
- Display trace output in the Log tab.

## Requirements

- Visual Studio 2022
- .NET Framework 4.6.2 developer pack
- Restored NuGet packages
- A running `PaymentTerminalService.Host` instance at the configured URL

## Build

From `src\PaymentTerminalService.TestApp1`:

```bat
nuget restore PaymentTerminalService.TestApp1.csproj
msbuild PaymentTerminalService.TestApp1.csproj /p:Configuration=Release
```

The test app can also be built from the solution.

## Typical Local Flow

1. Start `PaymentTerminalService.Host` in console mode.
2. Start `PaymentTerminalService.TestApp1`.
3. Click `Load terminals`.
4. Choose a terminal and connection.
5. Fill connection settings, such as serial port name.
6. Click `Select terminal`.
7. Exercise status, session, purchase, abort, loyalty, prompt, and confirm operations.

This project is for local development only and currently has no shared CI/CD pipeline.
