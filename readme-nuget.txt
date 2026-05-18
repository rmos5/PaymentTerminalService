PaymentTerminalService NuGet notes
=================================

Packages
--------

PaymentTerminalService.Model
    Shared DTOs, interfaces, exceptions, base terminal abstractions, and helpers.

PaymentTerminalService.Client
    POS-facing client library with PaymentTerminalServiceManager and terminal
    status polling.


Versioning
----------

Package versions should match the product version in:

    src\Version.props

The package `.nuspec` files currently carry explicit versions. When changing
`Version.props` for a package release, update the matching `.nuspec` versions
and run:

    powershell -ExecutionPolicy Bypass -File src\Validate-NuGetPackageVersions.ps1


Local pack examples
-------------------

From the repository root:

    .\nuget.exe restore src\PaymentTerminalService.sln

    .\nuget.exe pack src\PaymentTerminalService.Model\PaymentTerminalService.Model.csproj -Build -Symbols -Properties Configuration=Release

    .\nuget.exe pack src\PaymentTerminalService.Client\PaymentTerminalService.Client.csproj -Build -Symbols -Properties Configuration=Release


Local push example
------------------

Replace the source name/URL and API key with the internal feed values:

    .\nuget.exe push PaymentTerminalService.Model.1.0.0.nupkg -Source "PharmadataPackages" -ApiKey <api-key>

    .\nuget.exe push PaymentTerminalService.Client.1.0.0.nupkg -Source "PharmadataPackages" -ApiKey <api-key>
