param(
    [string]$SourceRoot
)

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = $PSScriptRoot
}

$versionProps = Join-Path $SourceRoot "Version.props"
$outputFile   = Join-Path $SourceRoot "GeneratedVersionInfo.cs"

[xml]$props = Get-Content $versionProps

$major = $props.Project.PropertyGroup.ProductMajor
$minor = $props.Project.PropertyGroup.ProductMinor
$patch = $props.Project.PropertyGroup.ProductPatch
$assemblyVersion = $props.Project.PropertyGroup.AssemblyVersion

$productVersion = "$major.$minor.$patch"
$generatedContent = @"
using System.Reflection;

[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$productVersion.0")]
[assembly: AssemblyInformationalVersion("$productVersion-local")]
"@

if (Test-Path $outputFile) {
    $existingContent = Get-Content $outputFile -Raw
    if ($existingContent -eq $generatedContent) {
        return
    }
}

$generatedContent | Set-Content $outputFile -Encoding UTF8
