param(
  [Parameter(Mandatory = $true)]
  [string]$SourceRoot,

  [Parameter(Mandatory = $true)]
  [string]$BuildSourceBranch,

  [Parameter(Mandatory = $true)]
  [string]$BuildSourceBranchName,

  [Parameter(Mandatory = $true)]
  [string]$BuildReason,

  [string]$PullRequestTargetBranch = ""
)

$ErrorActionPreference = "Stop"

$versionPropsPath = Join-Path $SourceRoot "Version.props"
if (-not (Test-Path $versionPropsPath)) {
  throw "Missing Version.props at '$versionPropsPath'. NuGet package version cannot be calculated."
}

[xml]$versionProps = Get-Content $versionPropsPath
$major = $versionProps.Project.PropertyGroup.ProductMajor
$minor = $versionProps.Project.PropertyGroup.ProductMinor
$patch = $versionProps.Project.PropertyGroup.ProductPatch
$packageVersion = "$major.$minor.$patch"

Write-Host "##vso[task.setvariable variable=packageVersion]$packageVersion"
Write-Host "NuGet package version: $packageVersion"

$changedFiles = @()
if ($BuildSourceBranch.StartsWith("refs/tags/v")) {
  $previousTag = git tag --sort=-creatordate --merged HEAD |
    Where-Object { $_ -like "v*" -and $_ -ne $BuildSourceBranchName } |
    Select-Object -First 1

  if ([string]::IsNullOrWhiteSpace($previousTag)) {
    $changedFiles = git diff-tree --no-commit-id --name-only -r HEAD
  }
  else {
    $changedFiles = git diff --name-only "$previousTag..HEAD"
  }
}
elseif ($BuildReason -eq "PullRequest") {
  if ([string]::IsNullOrWhiteSpace($PullRequestTargetBranch) -or $PullRequestTargetBranch.StartsWith('$(')) {
    throw "Build reason is PullRequest, but PullRequestTargetBranch was not provided."
  }

  $targetBranch = $PullRequestTargetBranch.Replace("refs/heads/", "")
  git fetch origin $targetBranch
  $mergeBase = git merge-base HEAD "origin/$targetBranch"
  $changedFiles = git diff --name-only "$mergeBase..HEAD"
}
else {
  $commitCount = [int](& git rev-list --count HEAD)
  if ($commitCount -gt 1) {
    $changedFiles = git diff --name-only "HEAD~1..HEAD"
  }
  else {
    $changedFiles = git diff-tree --no-commit-id --name-only -r HEAD
  }
}

$changedFiles = @($changedFiles | ForEach-Object { $_.Replace("\", "/") })
$modelPackageChanged = $false
$clientPackageChanged = $false

foreach ($file in $changedFiles) {
  if ($file -like "src/PaymentTerminalService.Model/*" -or
      $file -eq "src/Version.props" -or
      $file -eq "src/SharedAssemblyInfo.cs" -or
      $file -eq "src/GeneratedVersionInfo.cs") {
    $modelPackageChanged = $true
    $clientPackageChanged = $true
  }

  if ($file -like "src/PaymentTerminalService.Client/*" -or
      $file -eq "src/Version.props" -or
      $file -eq "src/SharedAssemblyInfo.cs" -or
      $file -eq "src/GeneratedVersionInfo.cs") {
    $clientPackageChanged = $true
  }
}

$anyPackageChanged = $modelPackageChanged -or $clientPackageChanged

Write-Host "Changed files:"
$changedFiles | ForEach-Object { Write-Host "  $_" }
Write-Host "Pack Model package: $modelPackageChanged"
Write-Host "Pack Client package: $clientPackageChanged"
Write-Host "##vso[task.setvariable variable=packModelPackage]$modelPackageChanged"
Write-Host "##vso[task.setvariable variable=packClientPackage]$clientPackageChanged"
Write-Host "##vso[task.setvariable variable=packAnyNuGetPackage]$anyPackageChanged"
Write-Host "##vso[task.setvariable variable=packAnyNuGetPackage;isOutput=true]$anyPackageChanged"
