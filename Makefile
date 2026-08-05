# TabletMidiPad packaging (GNU Make on Windows).
# Requires: dotnet, 7-Zip (7z on PATH or set SEVENZIP), NSIS (resolved at run time, or set MAKENSIS).
# From repo root: make help | make archive | make installer | make release

.PHONY: help publish archive uninstall-manifest installer release clean

CONFIG ?= Release
RUNTIME ?= win-x64
# Set SELFCONTAINED=1 for a self-contained publish (larger output, no separate .NET runtime on target).
SELFCONTAINED ?= 0

PROJECT := TabletMidiPad.csproj
PUBLISH := artifacts/publish
ARTIFACTS := artifacts
NSI := installer/TabletMidiPad.nsi
NSIS_BUILD_SCRIPT := installer/Build-Installer.ps1
ARCHIVE_BUILD_SCRIPT := installer/Build-Archive.ps1
UNINSTALL_MANIFEST := installer/UninstallFiles.generated.nsh
UNINSTALL_MANIFEST_SCRIPT := installer/Generate-UninstallManifest.ps1
RELEASE_CHECK_SCRIPT := scripts/Test-PublicRelease.ps1
CHECKSUM_SCRIPT := scripts/Write-ReleaseChecksums.ps1

DOTNET := dotnet
# Optional full path to 7z.exe. Empty = resolve via Build-Archive.ps1.
SEVENZIP ?=
# Optional full path to makensis.exe. Empty = resolve via Build-Installer.ps1.
MAKENSIS ?=
REPO_ROOT := $(subst \,/,$(CURDIR))
RUN_MAKENSIS = powershell -NoProfile -ExecutionPolicy Bypass -File "$(NSIS_BUILD_SCRIPT)" -RepositoryRoot "$(REPO_ROOT)" -MakeNsisPath "$(MAKENSIS)"

ifeq ($(SELFCONTAINED),1)
SC := true
else
SC := false
endif

STAMP := $(shell powershell -NoProfile -Command "Get-Date -Format 'yyyyMMdd-HHmmss'")
ARCHIVE := $(ARTIFACTS)/TabletMidiPad-$(CONFIG)-$(RUNTIME)-$(STAMP).7z
SETUP := $(ARTIFACTS)/TabletMidiPad-Setup.exe
RUN_ARCHIVE := powershell -NoProfile -ExecutionPolicy Bypass -File "$(ARCHIVE_BUILD_SCRIPT)" -PublishDirectory "$(PUBLISH)" -OutputPath "$(ARCHIVE)" -SevenZipPath "$(SEVENZIP)"
WRITE_CHECKSUMS := powershell -NoProfile -ExecutionPolicy Bypass -File "$(CHECKSUM_SCRIPT)" -ArchivePath "$(ARCHIVE)" -InstallerPath "$(SETUP)" -OutputPath "$(ARTIFACTS)/SHA256SUMS.txt"

# Single publish command reused in recipes (release chains one publish + archive + installer).
PUBLISH_CMD := $(DOTNET) publish $(PROJECT) -c $(CONFIG) -o $(PUBLISH) -r $(RUNTIME) --verbosity minimal --self-contained $(SC) -p:DebugType=None -p:DebugSymbols=false
GENERATE_UNINSTALL_MANIFEST := powershell -NoProfile -ExecutionPolicy Bypass -File "$(UNINSTALL_MANIFEST_SCRIPT)" -PublishDirectory "$(PUBLISH)" -OutputPath "$(UNINSTALL_MANIFEST)"
VERIFY_RELEASE := powershell -NoProfile -ExecutionPolicy Bypass -File "$(RELEASE_CHECK_SCRIPT)" -RepositoryRoot "$(REPO_ROOT)" -PublishDirectory "$(PUBLISH)"
CLEAN_PUBLISH := powershell -NoProfile -Command "if (Test-Path -LiteralPath '$(PUBLISH)') { Remove-Item -Recurse -Force '$(PUBLISH)' }"

help:
	@echo TabletMidiPad build targets:
	@echo   make publish       - dotnet publish to $(PUBLISH)
	@echo   make archive       - publish then 7z archive under $(ARTIFACTS)/
	@echo   make installer     - publish then NSIS $(SETUP)
	@echo   make release       - one publish, then archive and installer [both outputs]
	@echo   make clean         - remove $(PUBLISH)
	@echo Options: CONFIG=$(CONFIG) RUNTIME=$(RUNTIME) SELFCONTAINED=1
	@echo Override tools: SEVENZIP="C:/Program Files/7-Zip/7z.exe" MAKENSIS="C:/Program Files (x86)/NSIS/makensis.exe"

publish:
	@$(CLEAN_PUBLISH)
	@$(PUBLISH_CMD)

archive: publish
	@$(VERIFY_RELEASE)
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@echo 7-Zip archive: $(ARCHIVE)
	@$(RUN_ARCHIVE)

uninstall-manifest: publish
	@$(GENERATE_UNINSTALL_MANIFEST)

installer: uninstall-manifest
	@$(VERIFY_RELEASE)
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@$(RUN_MAKENSIS)

release:
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@$(CLEAN_PUBLISH)
	@$(PUBLISH_CMD)
	@$(GENERATE_UNINSTALL_MANIFEST)
	@$(VERIFY_RELEASE)
	@$(RUN_ARCHIVE)
	@$(RUN_MAKENSIS)
	@$(WRITE_CHECKSUMS)
	@echo Archive: $(ARCHIVE)
	@echo Setup: $(SETUP)
	@echo Checksums: $(ARTIFACTS)/SHA256SUMS.txt

clean:
	@powershell -NoProfile -Command "if (Test-Path -LiteralPath '$(PUBLISH)') { Remove-Item -Recurse -Force '$(PUBLISH)' }; if (Test-Path -LiteralPath '$(UNINSTALL_MANIFEST)') { Remove-Item -Force '$(UNINSTALL_MANIFEST)' }"
