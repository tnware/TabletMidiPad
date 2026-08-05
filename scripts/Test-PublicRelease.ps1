[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $RepositoryRoot 'artifacts\publish'
}

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        $failures.Add($Message)
    }
}

$installer = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'installer\TabletMidiPad.nsi')
$ownedFiles = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'installer\OwnedFiles.txt')
$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'TabletMidiPad.csproj')
$readme = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'README.md')

Assert-True ($installer -notmatch '(?im)^\s*!define\s+MUI_FINISHPAGE_RUN\b') 'Elevated installer offers to launch TabletMidiPad with the installer token.'
$legacyUninstallerShortcut = 'Delete "$SMPROGRAMS\TabletMidiPad\Uninstall TabletMidiPad.lnk"'
Assert-True (([regex]::Matches($installer, [regex]::Escape($legacyUninstallerShortcut))).Count -ge 2) 'Installer and uninstaller must remove the historical unsafe uninstaller shortcut.'
Assert-True ($installer -match '(?s)Section\s+"Uninstall".*?ReadRegStr\s+\$0\s+HKLM.+?InstallLocation.*?StrCmp\s+\$0\s+"\$INSTDIR".*?Delete\s+"\$SMPROGRAMS\\TabletMidiPad\\Uninstall TabletMidiPad\.lnk"') 'Historical shared uninstall shortcut deletion is not gated by registered InstallLocation ownership.'
Assert-True ($installer -notmatch '(?im)^\s*RMDir\s+/r\s+"\$INSTDIR"') 'Installer recursively deletes $INSTDIR.'
Assert-True ($installer -match 'UninstallFiles\.generated\.nsh') 'Installer does not consume an explicit generated uninstall manifest.'
Assert-True ($installer -match 'Function\s+RemovePreviousOwnedFiles') 'Installer does not clean cumulative application-owned files during upgrades.'
Assert-True ($installer -match 'ReadRegStr\s+\$0\s+HKLM.+InstallLocation') 'Upgrade cleanup is not gated by the registered installation path.'
Assert-True ($installer -match '!insertmacro\s+DeleteTabletMidiPadStaleFiles') 'Installer does not invoke cumulative stale-file cleanup during upgrades.'
Assert-True ($installer -match '(?s)Section\s+"Uninstall".*?ReadRegStr\s+\$0\s+HKLM.+?InstallLocation.*?StrCmp\s+\$0\s+"\$INSTDIR".*?DeleteRegKey\s+HKLM') 'Uninstaller can remove shared shortcuts or registry metadata owned by another installation.'
Assert-True ($ownedFiles -match '(?m)^TabletMidiPad\.pdb\r?$') 'Owned-file ledger omits the historical application PDB.'
Assert-True ($ownedFiles -match '(?m)^TabletMidiPad\.Core\.pdb\r?$') 'Owned-file ledger omits the historical core PDB.'
Assert-True ($project -match '(?s)<None Include="LICENSE".*?CopyToPublishDirectory="PreserveNewest"') 'Project LICENSE is not copied to publish output.'
Assert-True ($project -match 'THIRD-PARTY-NOTICES\.txt') 'Third-party notices are not copied to publish output.'
Assert-True ($readme -notmatch 'C:\\Users\\') 'README contains an absolute local user path.'
Assert-True ($readme -notmatch '(?i)target-application gating|only send MIDI while.*target app') 'README advertises removed target-app gating.'
Assert-True ($readme -notmatch '(?i)always-on-top|keep window always on top') 'README advertises removed always-on-top behavior.'
Assert-True ($readme -notmatch '(?i)running (the app|TabletMidiPad) as administrator') 'README recommends elevating the main application.'
Assert-True ($readme -match '(?i)takes priority control|priority Wintab') 'README lacks the tablet-control warning.'

$planPath = Join-Path $RepositoryRoot '.cursor\plans\mainwindow-next-cleanup.md'
Assert-True (-not (Test-Path -LiteralPath $planPath)) 'Stale internal Cursor plan is still present.'

if (Test-Path -LiteralPath $PublishDirectory) {
    Assert-True (Test-Path -LiteralPath (Join-Path $PublishDirectory 'LICENSE')) 'Publish output does not contain LICENSE.'
    Assert-True (Test-Path -LiteralPath (Join-Path $PublishDirectory 'THIRD-PARTY-NOTICES.txt')) 'Publish output does not contain THIRD-PARTY-NOTICES.txt.'
    $pdbs = @(Get-ChildItem -LiteralPath $PublishDirectory -Filter '*.pdb' -File -Recurse)
    Assert-True ($pdbs.Count -eq 0) 'Publish output contains PDB files.'
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Public-release safety checks passed.'
