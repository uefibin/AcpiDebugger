# ACPICA native backend

This directory contains the ACPICA source used by ACPI Debugger. The source was obtained from the official `open-acpica/acpica` repository at commit `98bbab7dbae0ff7941c4cf08d46f5d30978f809e`.

`integration/AcpiCompilerHost.vcxproj` builds the compiler and disassembler as `AcpiCompilerHost.dll`. The project is built for both `x64` and `ARM64`, and exports:

- `AcpiCompilerHost_Compile`
- `AcpiCompilerHost_Disassemble`

The WPF application calls these exports directly through P/Invoke. No external `iasl.exe` process is required for AML/ASL work. The Microsoft `asl.exe` tool remains a separate, optional dependency only for loading ACPI overrides.

The generated Flex/Bison C sources under `generate/msvc2017` are checked in so a normal Visual Studio build does not require Flex, Bison, or M4 at build time.
