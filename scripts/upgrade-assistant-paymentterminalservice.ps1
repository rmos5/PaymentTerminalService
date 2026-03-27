$projects = @(
    @{ Path = "src\PaymentTerminalService.Model\PaymentTerminalService.Model.csproj";   Sdk = "Microsoft.NET.Sdk";              WPF = $false },
    @{ Path = "src\PaymentTerminalService.Client\PaymentTerminalService.Client.csproj"; Sdk = "Microsoft.NET.Sdk";              WPF = $false },
    @{ Path = "src\PaymentTerminalService.Host\PaymentTerminalService.Host.csproj";     Sdk = "Microsoft.NET.Sdk";              WPF = $false },
    @{ Path = "src\PaymentTerminalService.Web\PaymentTerminalService.Web.csproj";       Sdk = "Microsoft.NET.Sdk";              WPF = $false },
    @{ Path = "src\PaymenTerminalService.TestApp1\PaymenTerminalService.TestApp1.csproj"; Sdk = "Microsoft.NET.Sdk.WindowsDesktop"; WPF = $true }
)

foreach ($proj in $projects) {
    $wpfLine = if ($proj.WPF) { "`n    <UseWPF>true</UseWPF>" } else { "" }
    $content = @"
<Project Sdk="$($proj.Sdk)">
  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>$wpfLine
  </PropertyGroup>
</Project>
"@
    Set-Content -Path $proj.Path -Value $content -Encoding UTF8
    Write-Host "Converted: $($proj.Path)"
}