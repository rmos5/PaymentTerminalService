# PaymentTerminalService

A self-hosted Windows Service that exposes a REST API for POS systems to interact with payment terminals.  
The service owns all terminal communication, safety, and recovery logic. POS applications are responsible for business workflow and user interaction only.

---

## Table of Contents

- [Overview](#overview)
- [Technology Stack](#technology-stack)
- [Architecture](#architecture)
- [Solution Structure](#solution-structure)
- [API Contract](#api-contract)
- [Configuration](#configuration)
- [Running the Service](#running-the-service)
- [Deployment](#deployment)
- [Logging](#logging)
- [Development & Testing](#development--testing)
- [NuGet Packages](#nuget-packages)
- [Contributing](#contributing)

---

## Overview

| Principle | Description |
|---|---|
| Single active terminal | Only one terminal is active at a time |
| Sequential access | No concurrent terminal control |
| Service-owned communication | The service manages all terminal I/O and error recovery |
| POS-owned workflow | POS/UI drives business logic and user interaction |
| Resilient | Robust against POS restarts, user switching, and long-running operations |
| REST + OpenAPI-first | API surface is defined by the OpenAPI contract |

---

## Technology Stack

| Component | Technology |
|---|---|
| Runtime | .NET Framework 4.6.2 |
| Language | C# |
| API hosting | OWIN self-host (HttpListener) |
| API protocol | JSON over HTTP |
| Service mode | Windows Service (same executable runs as console) |
| API documentation | OpenAPI / Swagger (Swashbuckle) |
| Dependency injection | SimpleInjector |
| Logging | NLog + NLog.Targets.Trace bridge |
| Terminal hardware | Verifone Yomani XR (via ECRTerminal SDK) |
| Installer | WiX Toolset v3 (MSI) |
| Unit testing | MSTest v2 |

---

## Architecture

```
┌──────────────────────────────────────┐
│             POS Application          │
│  (owns business workflow, user I/O)  │
└────────────────┬─────────────────────┘
                 │  HTTP / REST
                 ▼
┌──────────────────────────────────────┐
│        PaymentTerminalService        │
│  ┌──────────────────────────────┐    │
│  │  OWIN Web API (controllers)  │    │
│  └──────────────┬───────────────┘    │
│                 │                    │
│  ┌──────────────▼───────────────┐    │
│  │  PaymentTerminalManagement   │    │
│  │  Service (IPaymentTerminal   │    │
│  │  Selector)                   │    │
│  └──────────────┬───────────────┘    │
│                 │                    │
│  ┌──────────────▼───────────────┐    │
│  │  IPaymentTerminal impl.      │    │
│  │  (e.g. VerifoneYomaniXR)     │    │
│  └──────────────┬───────────────┘    │
└─────────────────┼────────────────────┘
                  │  Serial / TCP
                  ▼
          Payment Terminal Hardware
```

---

## Solution Structure

```
PaymentTerminalService/
├── src/
│   ├── PaymentTerminalService.Model/       # Shared contracts: DTOs, interfaces, exceptions, base classes
│   ├── PaymentTerminalService.Host/        # Executable: Windows Service / console host, business logic
│   │   └── Terminals/                      # Terminal implementations (e.g. VerifoneYomaniXR, TestTerminal)
│   ├── PaymentTerminalService.Web/         # Web API controllers, ApiExceptionFilter
│   ├── PaymentTerminalService.Client/      # Client library (NuGet package)
│   └── PaymentTerminalService.Installer/   # WiX installer
├── tests/
│   └── PaymentTerminalService.Tests/       # Unit tests
├── openapi/
│   └── PaymentTerminalService.v1.openapi.yaml   # OpenAPI contract (source of truth)
├── docs/                                   # Additional documentation
└── scripts/                                # PowerShell install/uninstall/firewall scripts
```

### Key Projects

#### "PaymentTerminalService.Model"
Shared contracts used across all projects:
- "IPaymentTerminalSelector" – terminal discovery, selection, and access
- "IPaymentTerminal" – payment terminal capabilities and operations
- DTOs: "TerminalCatalogResponse", "SelectedTerminalResponse", "SelectTerminalRequest", etc.
- Custom API exception types ("ApiNotFoundException", "ApiConflictException", etc.)
- "PaymentTerminalBase" – base class for terminal implementations
- "FileSessionStorageProvider", "TimestampFileNameProvider" – session persistence utilities
- Also distributed as the **"PaymentTerminalService.Model"** NuGet package

#### "PaymentTerminalService.Host"
Executable host project:
- Runs as a **Windows Service** or **console application** (same binary)
- Enforces single-instance via a named "Mutex" (exits with code "2" if already running)
- OWIN self-hosted Web API ("Startup.cs") with JSON-only formatting
- Registers "PaymentTerminalManagementService" as the "IPaymentTerminalSelector" singleton
- Restores the persisted terminal selection on startup
- Serves the raw OpenAPI YAML at "GET /apidoc"
- Swagger UI available at "/swagger/ui/index" — enabled always in "DEBUG", or when the "PAYMENT_TERMINAL_SERVICE_SWAGGER_ENABLED=1" environment variable is set in production
- Contains all terminal implementations under "Terminals/"

#### "PaymentTerminalService.Web"
Web API layer:
- "PaymentTerminalServiceController" – handles all API routes
- "ApiExceptionFilter" – maps typed exceptions to consistent HTTP error responses ("ErrorResponse")

#### "PaymentTerminalService.Client"
Reusable client library for POS integration, distributed as a NuGet package:
- "PaymentTerminalServiceManager" – high-level stateful manager; wraps the HTTP client and manages terminal status polling automatically after each operation
- "TerminalStatusPoller" – polls terminal status; fires "StatusReceived" events for both intermediate and final states
- Default polling interval: 3 s, start delay: 1 s

#### "PaymentTerminalService.Installer"
WiX v3-based MSI installer:
- Installs the service under "%ProgramFiles%\ProVersa\PaymentTerminalService"
- Registers and starts the Windows service automatically ("Account=LocalSystem", "Start=auto")
- Stops the service on upgrade/uninstall and removes it cleanly on uninstall
- Supports major upgrades (downgrades also allowed)
- Bundles all required assemblies: OWIN, Web API, SimpleInjector, NLog, Newtonsoft.Json, Verifone ECRTerminal SDK, and BCL polyfills

---

## API Contract

The OpenAPI specification is the **single source of truth** for all API changes:

```
openapi/PaymentTerminalService.v1.openapi.yaml
```

It defines:
- All endpoints and HTTP methods
- Request/response DTOs
- Terminal status and state model
- Error response structure and HTTP status codes (400, 404, 409, 500, …)

The raw YAML is also served at runtime from "GET /apidoc".

> **Do not change the API surface without updating the OpenAPI contract first.**

---

## Configuration

All configuration is read from `App.config` (`appSettings`).  
A config transform (`App.Release.config`) is applied automatically during **Release** builds, overriding development defaults with production values.

| Key | `App.config` (Debug) | `App.Release.config` (Release) | Description |
|---|---|---|---|
| `BaseUrl` | `http://127.0.0.1:7575/` | `http://127.0.0.1:7777/` | The URL the OWIN host listens on |
| `LogDirectory` | *(empty — falls back to `<BaseDir>\Logs`)* | `/ProVersa/PaymentTerminalService/Logs` | Directory where log files are written |
| `TerminalSessionDirectory` | *(empty — falls back to `<BaseDir>\TerminalSessions`)* | `/ProVersa/PaymentTerminalService/TerminalSessions` | Directory for persisted terminal session state |

### Environment variables

| Variable | Description |
|---|---|
| `PAYMENT_TERMINAL_SERVICE_SWAGGER_ENABLED` | Set to `1` to enable Swagger UI in Release builds |

### Terminal Catalog ("terminals.json")

Terminal discovery and selection state are persisted in "terminals.json" in the "TerminalSessionDirectory":

```json
{
  "terminals": [
    {
      "terminalId": "YOMANI-001",
      "vendor": "YOMANI",
      "model": "Yomani XR",
      "displayName": "Yomani Payment Terminal",
      "connections": [ ... ]
    }
  ],
  "selectedTerminalId": "YOMANI-001"
}
```

---

## Running the Service

### Console mode (development / debugging)

```bash
PaymentTerminalService.Host.exe
```

The service runs interactively in the console window. Press "Enter" to stop.

### Windows Service mode (production)

Install using the MSI built from "PaymentTerminalService.Installer":

### Mutex guard

Only one instance of the service may run at a time. A second launch exits immediately with exit code "2".

---

## Deployment

### Loopback / localhost (default)

Default binding: "http://127.0.0.1:7777/  
No firewall rule is required for loopback access.

## Logging

- Logging is provided by **NLog** with an "NLog.Targets.Trace" bridge so that all "System.Diagnostics.Trace" calls are routed through NLog automatically.
- Logs are written to daily, size-archived files in the configured "LogDirectory" (default: "<InstallDir>\Logs").
- Log entries cover service lifecycle events, terminal operations, errors, and diagnostic traces.
- The log directory can be overridden by passing a path as the first command-line argument on startup.

---

## Development & Testing

### Prerequisites

- Visual Studio 2022
- .NET Framework 4.6.2 SDK
- WiX Toolset v3 (for building the installer)
- Verifone.ECRTerminal library (for "VerifoneYomaniXRTerminal")

### Building

```bash
msbuild PaymentTerminalService.sln /p:Configuration=Release
```

### Running tests

```bash
vstest.console src\tests\PaymentTerminalService.Tests\bin\Release\PaymentTerminalService.Tests.dll
```

Test projects are located under "src/tests/" and cover:
- Model conversions and equality
- API exception filter behaviour
- Session storage provider
- File name providers and utilities

Coverage areas:

| Test class | Area |
|---|---|
| "ApiModelsEqualityTests" | DTO equality and model contracts |
| "MoneyConversionsTests" | Currency/money conversion logic |
| "TerminalStatusToStringTests" | Terminal status string formatting |
| "CollectionEqualityTests" | Collection extension utilities |
| "DictionaryPropertyExtensionsTests" | Dictionary property helpers |
| "FileSessionStorageProviderTests" | Session file persistence |
| "TimestampFileNameProviderTests" | Session file naming |
| "ApiExceptionFilterTests" | HTTP error response mapping |

### Test terminal

A "TestPaymentTerminal" implementation (with a separate "TestPaymentTerminal.Simulation.cs") is included for local development and simulation without physical hardware.

---

## NuGet Packages

Two packages are published to the internal **PharmadataPackages** NuGet feed:

| Package | Description |
|---|---|
| "PaymentTerminalService.Model" | Shared interfaces, DTOs, and base abstractions |
| "PaymentTerminalService.Client" | POS client library ("PaymentTerminalServiceManager", "TerminalStatusPoller") |
