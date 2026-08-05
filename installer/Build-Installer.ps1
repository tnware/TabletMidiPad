[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$MakeNsisPath,
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$nsiPath = Join-Path $repositoryFullPath 'installer\TabletMidiPad.nsi'
if (-not (Test-Path -LiteralPath $nsiPath -PathType Leaf)) {
    throw "NSIS script not found: $nsiPath"
}

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $repositoryFullPath 'artifacts\publish'
} elseif (-not [System.IO.Path]::IsPathRooted($PublishDirectory)) {
    $PublishDirectory = Join-Path $repositoryFullPath $PublishDirectory
}

$manifestScript = Join-Path $repositoryFullPath 'installer\Generate-UninstallManifest.ps1'
$manifestPath = Join-Path $repositoryFullPath 'installer\UninstallFiles.generated.nsh'
& $manifestScript -PublishDirectory $PublishDirectory -OutputPath $manifestPath

$executable = $null
if (-not [string]::IsNullOrWhiteSpace($MakeNsisPath)) {
    $executable = [System.IO.Path]::GetFullPath($MakeNsisPath)
} else {
    $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if (-not $command) {
        $command = Get-Command makensis -ErrorAction SilentlyContinue
    }

    if ($command) {
        $executable = $command.Source
    }

    if (-not $executable) {
        $candidates = @(
            (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'NSIS\makensis.exe'),
            (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'NSIS\makensis.exe')
        )
        $executable = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}

if (-not $executable -or -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'makensis.exe was not found. Install NSIS or set MAKENSIS to its absolute path.'
}

Write-Host "$executable /WX $nsiPath"
& $executable /WX $nsiPath
if ($LASTEXITCODE -ne 0) {
    throw "makensis.exe failed with exit code $LASTEXITCODE."
}
