# TabletMidiPad packaging (GNU Make on Windows).
# Requires: dotnet, 7-Zip (7z on PATH or set SEVENZIP), NSIS (resolved at run time, or set MAKENSIS).
# From repo root: make help | make archive | make installer | make release

.PHONY: help publish archive installer release clean

CONFIG ?= Release
RUNTIME ?= win-x64
# Set SELFCONTAINED=1 for a self-contained publish (larger output, no separate .NET runtime on target).
SELFCONTAINED ?= 0

PROJECT := TabletMidiPad.csproj
PUBLISH := artifacts/publish
ARTIFACTS := artifacts
NSI := installer/TabletMidiPad.nsi

DOTNET := dotnet
# 7-Zip: resolved at makefile parse time (often weak under Make's subshell); override SEVENZIP if archive fails.
SEVENZIP ?= $(shell powershell -NoProfile -Command "$$c=(Get-Command 7z -ErrorAction SilentlyContinue).Source; $$pf=Join-Path $$env:ProgramFiles '7-Zip\\7z.exe'; $$pfx86=Join-Path $$([Environment]::GetFolderPath('ProgramFilesX86')) '7-Zip\\7z.exe'; if ($$c) { $$c } elseif (Test-Path -LiteralPath $$pf) { $$pf } elseif (Test-Path -LiteralPath $$pfx86) { $$pfx86 } else { '7z' }")
# Optional full path to makensis.exe. Empty = resolve in RUN_MAKENSIS (PATH + Program Files + Program Files (x86)).
MAKENSIS ?=
REPO_ROOT := $(subst \,/,$(CURDIR))
# Run NSIS with absolute script path; CreateProcess fails if makensis is not on PATH for cmd (see installer target).
RUN_MAKENSIS = powershell -NoProfile -ExecutionPolicy Bypass -Command "$$root='$(REPO_ROOT)'; $$nsi=Join-Path $$root '$(NSI)'; $$o='$(MAKENSIS)'; if ($$o) { $$exe=$$o } else { $$exe=(Get-Command makensis -ErrorAction SilentlyContinue).Source; if (-not $$exe) { $$p=Join-Path $$([Environment]::GetFolderPath('ProgramFilesX86')) 'NSIS\\makensis.exe'; if (Test-Path -LiteralPath $$p) { $$exe=$$p } }; if (-not $$exe) { $$p=Join-Path $$env:ProgramFiles 'NSIS\\makensis.exe'; if (Test-Path -LiteralPath $$p) { $$exe=$$p } } }; if (-not $$exe) { Write-Host 'makensis not found. Install NSIS or set MAKENSIS to the full path to makensis.exe' -ForegroundColor Red; exit 1 }; if (-not (Test-Path -LiteralPath $$exe)) { Write-Host ('Not found: ' + $$exe); exit 1 }; Write-Host ($$exe + ' ' + $$nsi); & $$exe $$nsi"

ifeq ($(SELFCONTAINED),1)
SC := true
else
SC := false
endif

STAMP := $(shell powershell -NoProfile -Command "Get-Date -Format 'yyyyMMdd-HHmmss'")
ARCHIVE := $(ARTIFACTS)/TabletMidiPad-$(CONFIG)-$(RUNTIME)-$(STAMP).7z
SETUP := $(ARTIFACTS)/TabletMidiPad-Setup.exe

# Single publish command reused in recipes (release chains one publish + archive + installer).
PUBLISH_CMD := $(DOTNET) publish $(PROJECT) -c $(CONFIG) -o $(PUBLISH) -r $(RUNTIME) --verbosity minimal --self-contained $(SC)

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
	@$(PUBLISH_CMD)

archive: publish
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@echo 7-Zip archive: $(ARCHIVE)
	@"$(SEVENZIP)" a -t7z -mx=9 -bso0 -r "$(ARCHIVE)" "$(PUBLISH)/*"

installer: publish
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@$(RUN_MAKENSIS)

release:
	@powershell -NoProfile -Command "New-Item -ItemType Directory -Path '$(ARTIFACTS)' -Force | Out-Null"
	@$(PUBLISH_CMD) && "$(SEVENZIP)" a -t7z -mx=9 -bso0 -r "$(ARCHIVE)" "$(PUBLISH)/*" && $(RUN_MAKENSIS)
	@echo Archive: $(ARCHIVE)
	@echo Setup: $(SETUP)

clean:
	@powershell -NoProfile -Command "if (Test-Path -LiteralPath '$(PUBLISH)') { Remove-Item -Recurse -Force '$(PUBLISH)' }"
