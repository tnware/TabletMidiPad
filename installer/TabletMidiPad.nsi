; TabletMidiPad Windows installer (NSIS 3.x)
; From repo root: make installer   or   make release
; Or after dotnet publish: makensis installer\TabletMidiPad.nsi
; Paths below are relative to this .nsi file's directory.
!include "VersionDefines.generated.nsh"

Unicode true
RequestExecutionLevel admin

!define APP_NAME "TabletMidiPad"
!define APP_PUBLISHER "TabletMidiPad"
!define MAIN_EXE "TabletMidiPad.exe"

; Published output from: dotnet publish -c Release -r win-x64 -o artifacts\publish
!define ARTIFACTS_DIR "..\artifacts\publish"

!if /FileExists "${ARTIFACTS_DIR}\${MAIN_EXE}"
!else
  !error "Missing ${ARTIFACTS_DIR}\${MAIN_EXE}. Run make publish (or dotnet publish) first."
!endif

Name "${APP_NAME}"
Caption "${APP_NAME} Setup"
OutFile "..\artifacts\TabletMidiPad-Setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"

Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

Section "Application" SecApp
  SetOutPath $INSTDIR
  File /r "${ARTIFACTS_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${MAIN_EXE}" "" "" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\${MAIN_EXE}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  RMDir /r "$INSTDIR"

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
SectionEnd
