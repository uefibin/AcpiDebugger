# Changelog

- Re-enabled the integrated Load Override backend after matching Microsoft `asl.exe` full-table replacement stream and registry-value boundary behavior, restoring the required firmware revision increment, and adding strict table length/checksum validation plus registry read-back verification.
- Changed Remove Override to clear every detected ACPI override revision plus `acpitabl.dat`, without requiring a selected table; unrelated ACPI parameter keys such as `WakeUp` are preserved.
- Removed the Microsoft `asl.exe` dependency from Load/Remove Override. The integrated loader reproduces `/loadtable` registry deltas (`offset`, `length`, changed bytes), value splitting, identifier normalization, and verification; removal also cleans the temporary `acpitabl.dat` mechanism used by an earlier build.

## Unreleased

- Replaced the `acpidump.exe` dump dependency with native Windows ACPI table extraction, including all SSDT instances exposed under `HKLM\HARDWARE\ACPI`.
- Integrated ACPICA source into `third_party/acpica` and added an in-process x64/ARM64 compiler/disassembler backend; AML/ASL operations no longer launch `iasl.exe`.
- Expanded Table-specific decoding using the Binio ACPI decoder as the reference, including detailed variable-length APIC, DBG2, DTPR, FADT/FACS, FIDT, FPDT, LPIT, MCFG, NHLT, PHAT, TPM2, UEFI, WSMT, and root-table fields with truncation reporting.

## 1.3.0 - 2026-07-23

- Added a Table-specific details tab alongside the common ACPI header view.
- Added structured field parsing for BGRT, APIC, DBG2, DBGP, FACP, FPDT, HPET, LPIT, MCFG, TPM2, UEFI, WSMT, and XSDT tables.
- Added safe AML payload summaries for DSDT/SSDT and generic payload previews for vendor-specific tables.
- Matched the Table-specific field layout and typography to the Common details tab.
- Expanded friendly table descriptions for common debugging, performance, security, and firmware tables.

## 1.2.0 - 2026-07-17

- Added a Remove Override action with an optional immediate Windows restart.
- Fixed ACPI override removal to target only the exact staged OEM revision, preserve padded ACPI identifiers, and avoid deleting unrelated registry revisions.
- Added removal verification, cancellation support, and an `asl.exe` timeout.
- Changed the About dialog to display the semantic product version.

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
