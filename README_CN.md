# ACPI Debugger

[English](README.md) | 中文

![ACPI Debugger](https://img.shields.io/badge/ACPI-Debugger-008C95)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Language](https://img.shields.io/badge/language-C%23%20%2F%20WPF-purple)
[![GitHub Stars](https://img.shields.io/github/stars/uefibin/AcpiDebugger?style=flat-square)](https://github.com/uefibin/AcpiDebugger/stargazers)
![License](https://img.shields.io/badge/license-MIT-green)

> ⭐ 如果 ACPI Debugger 对你的 BIOS/UEFI 开发有帮助，欢迎 Star 支持项目。

[![Star History](https://api.star-history.com/svg?repos=uefibin/AcpiDebugger&type=Date)](https://star-history.com/#uefibin/AcpiDebugger&Date)

ACPI Debugger 是一款面向 BIOS/UEFI 工程师的 Windows ACPI 开发与调试工具，提供 ACPI 表导出、AML 反编译、ASL 编辑、AML 编译及 Windows ACPI Override 加载的一站式工作流。

## 功能特点

- 浏览 ACPI Table（DSDT / SSDT）
- AML 反编译为 ASL
- ASL 代码编辑与语法高亮
- iASL 编译、诊断解析和错误定位
- 批量反编译 / 编译及整体进度显示
- ACPI Table 信息查看
- Windows ACPI Override 加载
- 管理员权限与 Test Mode 检测
- 响应式 WPF 界面

## 使用流程

```text
Dump ACPI Tables
        ↓
Decompile AML
        ↓
修改 ASL
        ↓
Compile AML
        ↓
Load Override
```

> 加载 Override 会修改 Windows 的 ACPI 配置，请仅在测试机器上操作，并提前准备系统恢复方案。

## 外部工具依赖

| 工具 | 来源 | 协议 | 用途 | 放置目录 |
|---|---|---|---|---|
| `iasl.exe` | [ACPICA](https://github.com/acpica/acpica) | BSD-3-Clause | AML 反编译、ASL 编译 | `AcpiDebuggerApp/tools/iasl.exe` |
| `acpidump.exe` | [ACPICA](https://github.com/acpica/acpica) | BSD-3-Clause | 导出 Windows ACPI Table | `AcpiDebuggerApp/tools/acpidump.exe` |
| `asl.exe` | [Microsoft ASL Compiler](https://learn.microsoft.com/windows-hardware/drivers/bringup/microsoft-asl-compiler) | Microsoft License | 加载 Windows ACPI Override Table | `AcpiDebuggerApp/tools/asl.exe` 或 `PATH` 中的目录 |

仓库当前已包含 `iasl.exe` 和 `acpidump.exe`。需要加载 Override 时，请从 Windows Driver Kit（WDK）获取 `asl.exe`。分发任何外部二进制文件前，请确认符合其许可证要求。

目录结构：

```text
AcpiDebuggerApp/
├─ AcpiDebugger/
│  └─ AcpiDebugger.csproj
└─ tools/
   ├─ iasl.exe
   ├─ acpidump.exe
   └─ asl.exe          # 仅加载 Override 时需要
```

## 开源组件

| 组件 | 协议 | 用途 |
|---|---|---|
| [ACPICA](https://github.com/acpica/acpica) | BSD-3-Clause | ACPI 工具链（`iasl` / `acpidump`） |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | MIT | ASL 代码编辑器 |
| [.NET / WPF](https://github.com/dotnet/wpf) | MIT | 应用框架 |

## 编译

环境要求：

- Windows 10 或 Windows 11
- .NET 9 SDK
- Visual Studio 2022 或兼容的构建环境

在仓库根目录执行：

```powershell
dotnet restore .\AcpiDebuggerApp\AcpiDebugger\AcpiDebugger.csproj
dotnet build .\AcpiDebuggerApp\AcpiDebugger\AcpiDebugger.csproj -c Release
```

导出 ACPI 表或加载 Override 时，请以管理员身份运行程序。

## 作者

**bin**

- 博客：[https://ay123.net](https://ay123.net)
- 微信公众号：**UEFI那点事**

## 开源协议

本项目采用 [MIT License](LICENSE)。第三方工具和组件仍遵循各自的许可证。
