# ACPI Debugger

[English](README.md) | [中文](README_CN.md)

![ACPI Debugger](https://img.shields.io/badge/ACPI-Debugger-008C95)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Language](https://img.shields.io/badge/language-C%23%20%2F%20WPF-purple)
[![GitHub Stars](https://img.shields.io/github/stars/uefibin/AcpiDebugger?style=flat-square)](https://github.com/uefibin/AcpiDebugger/stargazers)
![License](https://img.shields.io/badge/license-MIT-green)

> ⭐ If ACPI Debugger helps your BIOS/UEFI development, consider giving the project a Star.

A modern Windows ACPI development and debugging tool for BIOS/UEFI engineers. It provides a complete workflow for dumping ACPI tables, decompiling AML, editing ASL, compiling AML, and loading Windows ACPI overrides.

## Features

- Browse ACPI tables (DSDT / SSDT)
- AML-to-ASL decompilation
- ASL editor with syntax highlighting
- iASL compilation, diagnostics, and error navigation
- Batch decompilation and compilation with overall progress
- ACPI table information viewer
- Table-specific fields for BGRT, APIC, FADT, HPET, MCFG, TPM2, debug, performance, security, and root tables
- Windows ACPI override loading
- Administrator privilege and Test Mode detection
- Responsive WPF interface

## Workflow

```text
Dump ACPI Tables
        ↓
Decompile AML
        ↓
Edit ASL
        ↓
Compile AML
        ↓
Load Override
```

> Loading an override changes Windows ACPI configuration. Use it only on a test system and make sure you have a recovery method available.

## External Tool Dependencies

| Tool | Source | License | Purpose | Required location |
|---|---|---|---|---|
| `iasl.exe` | [ACPICA](https://github.com/acpica/acpica) | BSD-3-Clause | AML disassembly and ASL compilation | `AcpiDebuggerApp/tools/iasl.exe` |
| `asl.exe` | [Microsoft ASL compiler](https://learn.microsoft.com/windows-hardware/drivers/bringup/microsoft-asl-compiler) | Microsoft license | Load ACPI override tables into Windows | `AcpiDebuggerApp/tools/asl.exe` or a directory in `PATH` |

Dumping uses the native Windows ACPI interfaces and the `HKLM\HARDWARE\ACPI` registry data, including every DSDT, FACS, FADT/XSDT, and SSDT instance, so `acpidump.exe` is not required. `iasl.exe` is still used for AML disassembly and ASL compilation; `asl.exe` is only needed for loading ACPI overrides. External executables are not included in this repository. Verify that use and redistribution of every external binary complies with its license.

Expected structure:

```text
AcpiDebuggerApp/
├─ AcpiDebugger/
│  └─ AcpiDebugger.csproj
└─ tools/
   ├─ iasl.exe
   └─ asl.exe          # optional unless loading overrides
```

## Open Source Components

| Component | License | Purpose |
|---|---|---|
| [ACPICA](https://github.com/acpica/acpica) | BSD-3-Clause | ACPI ASL/AML toolchain (`iasl`) |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | MIT | ASL code editor |
| [.NET / WPF](https://github.com/dotnet/wpf) | MIT | Application framework |

## Build

Requirements:

- Windows 10 or Windows 11
- .NET 9 SDK
- Visual Studio 2022 or another compatible build environment

From the repository root:

```powershell
dotnet restore .\AcpiDebuggerApp\AcpiDebugger\AcpiDebugger.csproj
dotnet build .\AcpiDebuggerApp\AcpiDebugger\AcpiDebugger.csproj -c Release
```

Run the application as administrator when dumping tables or loading overrides.

## Author

**bin**

- Blog: [https://ay123.net](https://ay123.net)
- WeChat: **UEFI那点事**

## License

This project is released under the [MIT License](LICENSE). Third-party tools and components remain subject to their respective licenses.
