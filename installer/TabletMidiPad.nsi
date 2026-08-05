Unicode true

!include "MUI2.nsh"
!include "VersionDefines.generated.nsh"
!include "UninstallFiles.generated.nsh"

!define APP_NAME "TabletMidiPad"
!define APP_PUBLISHER "Tyler Woods"
!define APP_URL "https://github.com/tnware/TabletMidiPad"
!define APP_EXE "TabletMidiPad.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define INSTALL_MARKER ".tabletmidipad-install"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\artifacts\TabletMidiPad-Setup.exe"
InstallDir "$PROGRAMFILES64\TabletMidiPad"
InstallDirRegKey HKLM "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetOverwrite on

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "Wacom tablet to MIDI controller"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 ${APP_PUBLISHER}"

!define MUI_ABORTWARNING
!define MUI_ICON "..\Assets\app.ico"
!define MUI_UNICON "..\Assets\app.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
; Keep a dedicated Program Files directory. /D overrides remain safe because
; uninstall deletes only the generated list of application-owned files.
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function RemovePreviousOwnedFiles
  ; Upgrade cleanup is allowed only for the HKLM-registered installation path
  ; containing the application executable. /D cannot authorize deletion in an
  ; arbitrary directory. The cumulative ledger never deletes unknown files.
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $0 "$INSTDIR" 0 cleanup_done
  IfFileExists "$INSTDIR\${APP_EXE}" 0 cleanup_done
  !insertmacro DeleteTabletMidiPadStaleFiles
cleanup_done:
FunctionEnd

Section "Install"
  Call RemovePreviousOwnedFiles
  SetOutPath "$INSTDIR"
  File /r "..\artifacts\publish\*.*"

  FileOpen $0 "$INSTDIR\${INSTALL_MARKER}" w
  FileWrite $0 "${APP_NAME} ${APP_VERSION}$\r$\n"
  FileClose $0

  ; Remove the legacy shortcut to the previous uninstaller. Leaving it behind
  ; could expose an older relocated uninstaller after this install takes ownership.
  Delete "$SMPROGRAMS\TabletMidiPad\Uninstall TabletMidiPad.lnk"
  CreateShortCut "$DESKTOP\TabletMidiPad.lnk" "$INSTDIR\${APP_EXE}"
  CreateDirectory "$SMPROGRAMS\TabletMidiPad"
  CreateShortCut "$SMPROGRAMS\TabletMidiPad\TabletMidiPad.lnk" "$INSTDIR\${APP_EXE}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" "$\"$INSTDIR\Uninstall.exe$\" /S"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Function un.onInit
  IfFileExists "$INSTDIR\${INSTALL_MARKER}" marker_found
  MessageBox MB_ICONSTOP|MB_OK "This folder is not a verified TabletMidiPad installation. Uninstall has been cancelled."
  Abort
marker_found:
FunctionEnd

Section "Uninstall"
  ; Generated from the cumulative application-owned ledger. This deliberately
  ; avoids RMDir /r so unknown files in the installation directory are preserved.
  !insertmacro DeleteTabletMidiPadOwnedFiles
  !insertmacro RemoveTabletMidiPadOwnedDirectories

  Delete "$INSTDIR\${INSTALL_MARKER}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  ; An older relocated installation must not remove shared metadata belonging
  ; to a newer installation. Only the currently registered path owns it.
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $0 "$INSTDIR" 0 shared_metadata_done
  Delete "$SMPROGRAMS\TabletMidiPad\Uninstall TabletMidiPad.lnk"
  Delete "$DESKTOP\TabletMidiPad.lnk"
  Delete "$SMPROGRAMS\TabletMidiPad\TabletMidiPad.lnk"
  RMDir "$SMPROGRAMS\TabletMidiPad"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
shared_metadata_done:
SectionEnd
