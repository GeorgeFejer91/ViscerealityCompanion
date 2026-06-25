<#
.SYNOPSIS
    Builds and launches the minimal peripersonal experiment runner.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Refresh,
    [switch]$Wait
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repoRoot 'src\ViscerealityCompanion.MinimalRunner\ViscerealityCompanion.MinimalRunner.csproj'
$framework = 'net10.0-windows10.0.19041.0'
$outputPath = Join-Path $repoRoot "src\ViscerealityCompanion.MinimalRunner\bin\$Configuration\$framework"
$exePath = Join-Path $outputPath 'ViscerealityMinimalRunner.exe'
$userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $userDotnet) { $userDotnet } else { 'dotnet' }

if ($Refresh -or -not (Test-Path $exePath)) {
    & $dotnet build $projectPath --configuration $Configuration | Out-Host
}

if (-not (Test-Path $exePath)) {
    throw "Minimal runner executable not found at $exePath"
}

$process = Start-Process -FilePath $exePath -WorkingDirectory $outputPath -PassThru
if ($Wait) {
    Wait-Process -Id $process.Id
}

$process | Select-Object Id, ProcessName, Path
