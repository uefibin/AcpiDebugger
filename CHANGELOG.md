# Changelog

## Unreleased

- Refactored ACPICA process execution into `AcpiToolService`.
- Added cancellable batch compile and decompile operations with overall progress.
- Added ACPI table parsing/checksum service and shared models.
- Added iASL diagnostic parser.
- Changed Error 6163 repair from silent automatic edits to a preview-and-confirm flow for single-file compilation.
- Added tool status reporting for `iasl.exe`, `acpidump.exe`, and Microsoft `asl.exe`.
- Added ACPI override service facade.
- Added project build and dependency documentation.
