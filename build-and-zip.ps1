#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes TabletMidiPad (Release) and archives the output with 7-Zip.

.DESCRIPTION
  Runs dotnet publish to artifacts/publish, then creates artifacts/TabletMidiPad-<cfg>-<rid>-<timestamp>.7z.
  Requires 7-Zip (7z.exe on PATH or under Program Files).

.PARAMETER Configuration
  MSBuild configuration (default Release).

.PARAMETER Runtime
  dotnet -r value (default win-x64).

.PARAMETER SelfContained
  If set, publishes a self-contained app (larger archive, no separate .NET install on target PC).

.EXAMPLE
  .\build-and-zip.ps1

.EXAMPLE
  .\build-and-zip.ps1 -SelfContained
#>
param(
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [switch] $SelfContained
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$csproj = Join-Path $root 'TabletMidiPad.csproj'
if (-not (Test-Path -LiteralPath $csproj)) {
    throw "Cannot find TabletMidiPad.csproj in $root"
}

function Resolve-7Zip {
    $fromPath = $null
    $cmd = Get-Command '7z' -ErrorAction SilentlyContinue
    if ($cmd) { $fromPath = $cmd.Source }
    $candidates = @(
        @(
            $fromPath
            (Join-Path $env:ProgramFiles '7-Zip\7z.exe')
            (Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe')
        ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    )
    if ($candidates.Count -eq 0) {
        throw '7-Zip not found. Install from https://www.7-zip.org/ or add 7z.exe to PATH.'
    }
    return $candidates[0]
}

$publishDir = Join-Path $root 'artifacts\publish'
$artifactsDir = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
    'publish', $csproj,
    '-c', $Configuration,
    '-o', $publishDir,
    '-r', $Runtime,
    '--verbosity', 'minimal'
)
if ($SelfContained) {
    $publishArgs += @('--self-contained', 'true')
} else {
    $publishArgs += @('--self-contained', 'false')
}

Write-Host "dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$sevenZip = Resolve-7Zip
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$zipName = "TabletMidiPad-$Configuration-$Runtime-$stamp.7z"
$zipPath = Join-Path $artifactsDir $zipName

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Write-Host "7-Zip: $sevenZip" -ForegroundColor Cyan
Write-Host "Archive: $zipPath" -ForegroundColor Cyan

$sevenZipArgs = @(
    'a',
    '-t7z',
    '-mx=9',
    '-bso0',
    '-r',
    $zipPath,
    (Join-Path $publishDir '*')
)
& $sevenZip @sevenZipArgs
if ($LASTEXITCODE -ne 0) {
    throw "7z failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Published: $publishDir" -ForegroundColor Green
Write-Host "Archive:    $zipPath" -ForegroundColor Green
