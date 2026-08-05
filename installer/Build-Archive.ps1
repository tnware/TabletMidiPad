[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$SevenZipPath
)

$ErrorActionPreference = 'Stop'
$publishFullPath = [System.IO.Path]::GetFullPath($PublishDirectory)
if (-not (Test-Path -LiteralPath $publishFullPath -PathType Container)) {
    throw "Publish directory not found: $publishFullPath"
}

$executable = $null
if (-not [string]::IsNullOrWhiteSpace($SevenZipPath)) {
    $executable = [System.IO.Path]::GetFullPath($SevenZipPath)
} else {
    $command = Get-Command 7z.exe -ErrorAction SilentlyContinue
    if (-not $command) {
        $command = Get-Command 7z -ErrorAction SilentlyContinue
    }

    if ($command) {
        $executable = $command.Source
    }

    if (-not $executable) {
        $candidates = @(
            (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) '7-Zip\7z.exe'),
            (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) '7-Zip\7z.exe')
        )
        $executable = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}

if (-not $executable -or -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw '7z.exe was not found. Install 7-Zip or set SEVENZIP to its absolute path.'
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $outputFullPath)) | Out-Null
if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Force
}

Write-Host "$executable a -t7z -mx=9 $outputFullPath $publishFullPath\*"
& $executable a -t7z -mx=9 -bso0 $outputFullPath "$publishFullPath\*"
if ($LASTEXITCODE -ne 0) {
    throw "7z.exe failed with exit code $LASTEXITCODE."
}
