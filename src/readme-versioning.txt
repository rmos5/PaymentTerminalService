PaymentTerminalService Versioning
=================================

Version source
--------------

`src\Version.props` is the source of truth for the product version:

    ProductMajor
    ProductMinor
    ProductPatch

The product version is:

    ProductMajor.ProductMinor.ProductPatch

Example:

    1.0.1

Change `src\Version.props` when an intentional new product installer or NuGet
package version is required.


Generated assembly version info
-------------------------------

`src\Generate-VersionInfo.ps1` reads `src\Version.props` and generates:

    src\GeneratedVersionInfo.cs

Generated assembly metadata:

    AssemblyVersion
        Comes from `AssemblyVersion` in `Version.props`.
        This is the .NET binary compatibility version and should change rarely.

    AssemblyFileVersion
        Uses the product version plus `.0`.
        Example: `1.0.1.0`

    AssemblyInformationalVersion
        Uses the product version with local suffix.
        Example: `1.0.1-local`


Installer versioning
--------------------

The WiX MSI and Burn bootstrapper import `src\Version.props`.

MSI ProductVersion:

    ProductMajor.ProductMinor.ProductPatch

Burn BundleVersion:

    ProductMajor.ProductMinor.ProductPatch

The MSI is built because the Burn EXE bootstrapper needs it. The MSI is an
internal build output and is not the deployment artifact.

The deployable installer is:

    PaymentTerminalService.Setup.x64.exe


Build artifact layout
---------------------

The deployable service artifact should be version-shaped:

    service-drop\<product-version>\PaymentTerminalService.Setup.x64.exe

Example:

    service-drop\1.0.1\PaymentTerminalService.Setup.x64.exe

Only the EXE bootstrapper should be published for service deployment.

No active Azure Pipelines YAML files are currently checked in under
`azure-pipelines\`.


Deployment versioning
---------------------

Service deployment is handled outside YAML by a manually configured classic
Release/CD pipeline.

The manually selected CI artifact should be published once to a shared version
folder, for example:

    \\server\deploy\PaymentTerminalService\1.0.1

TEST and PRODUCTION/customer deployment must consume the same shared version
folder and the same EXE.

CD must not rebuild the product.


NuGet package versioning
------------------------

NuGet package versions also come from `src\Version.props`.

Before publishing packages, validate that package `.nuspec` versions match the
product version from `Version.props`:

    powershell -ExecutionPolicy Bypass -File src\Validate-NuGetPackageVersions.ps1

Packages are packed only when package-relevant code changes.

Package-relevant projects:

    PaymentTerminalService.Model
    PaymentTerminalService.Client

Update the related `.nuspec` files when `Version.props` is changed for a
package release.


Local development
-----------------

Local builds use the version currently stored in `src\Version.props`.

Example:

    1.0.1

After changing `Version.props`, regenerate version info or rebuild the solution
so `GeneratedVersionInfo.cs` is updated.

Visual Studio may need a project reload or solution reopen for WiX project
property changes to be fully refreshed.


Version change rules
--------------------

Patch release:

    1.0.0 -> 1.0.1

Minor release:

    1.0.1 -> 1.1.0

Major release:

    1.1.0 -> 2.0.0

`AssemblyVersion` should stay stable unless binary compatibility changes.


Rollback rule
-------------

Do not decrease version numbers.

Bad:

    1.1.0 -> 1.0.0

Good:

    restore older code if needed, but publish it as a newer version:

    1.1.1
