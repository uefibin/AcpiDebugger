# Changelog

## Unreleased

## 1.1.0 - 2026-07-13

- Added a new Windows application icon and applied it to the executable, taskbar, main window, and About window.
- Automatically switch to the Output tab whenever an operation starts or writes log output.
- Added timestamped operation-start logging for dump, compile, decompile, batch, and override actions.
- Refactored ACPICA process execution into `AcpiToolService`.
- Added cancellable batch compile and decompile operations with overall progress.
- Added ACPI table parsing/checksum service and shared models.
- Added iASL diagnostic parser.
- Changed Error 6163 repair from silent automatic edits to a preview-and-confirm flow for single-file compilation.
- Added tool status reporting for `iasl.exe`, `acpidump.exe`, and Microsoft `asl.exe`.
- Added ACPI override service facade.
- Added project build and dependency documentation.
