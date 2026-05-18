param(
    [string]$SourceRoot
)

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = $PSScriptRoot
}

$versionPropsPath = Join-Path $SourceRoot "Version.props"

if (!(Test-Path $versionPropsPath)) {
    throw "Version.props was not found at '$versionPropsPath'."
}

[xml]$versionProps = Get-Content $versionPropsPath

$major = $versionProps.Project.PropertyGroup.ProductMajor
$minor = $versionProps.Project.PropertyGroup.ProductMinor
$patch = $versionProps.Project.PropertyGroup.ProductPatch
$productVersion = "$major.$minor.$patch"

$nuspecs = @(
    @{
        Package = "PaymentTerminalService.Model"
        Path = Join-Path $SourceRoot "PaymentTerminalService.Model\PaymentTerminalService.Model.nuspec"
    },
    @{
        Package = "PaymentTerminalService.Client"
        Path = Join-Path $SourceRoot "PaymentTerminalService.Client\PaymentTerminalService.Client.nuspec"
    }
)

$errors = @()

foreach ($nuspec in $nuspecs) {
    if (!(Test-Path $nuspec.Path)) {
        $errors += "$($nuspec.Package): nuspec file was not found at '$($nuspec.Path)'."
        continue
    }

    [xml]$nuspecXml = Get-Content $nuspec.Path
    $nuspecVersion = $nuspecXml.package.metadata.version

    if ([string]::IsNullOrWhiteSpace($nuspecVersion)) {
        $errors += "$($nuspec.Package): nuspec version is empty. Expected '$productVersion'."
        continue
    }

    if ($nuspecVersion -ne $productVersion) {
        $errors += "$($nuspec.Package): nuspec version '$nuspecVersion' does not match Version.props product version '$productVersion'. Update '$($nuspec.Path)' or 'Version.props' before packing."
    }
}

if ($errors.Count -gt 0) {
    $message = "NuGet package version validation failed:`n" + ($errors -join "`n")
    throw $message
}

Write-Host "NuGet package versions match Version.props product version '$productVersion'."
