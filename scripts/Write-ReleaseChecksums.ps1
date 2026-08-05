[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$files = @(
    [System.IO.Path]::GetFullPath($ArchivePath),
    [System.IO.Path]::GetFullPath($InstallerPath)
)

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Release artifact not found: $file"
    }
}

$lines = $files | ForEach-Object {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($_))"
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $outputFullPath)) | Out-Null
[System.IO.File]::WriteAllLines($outputFullPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote SHA-256 checksums: $outputFullPath"
