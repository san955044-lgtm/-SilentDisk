# 🫪 SilentDisk

Ultra-lightweight disk health and storage monitor for Windows & Windows Server.

## Features
- **Zero Dependencies:** Compiles natively via built-in `csc.exe` (no Visual Studio needed).
- **Physical Drive Health:** WMI SMART status monitoring for SSD, NVMe, and USB.
- **Dynamic Tray Status:**  Green (Healthy) |  Orange (Low Space) |  Red (Hardware Error).
- **OS Support:** Windows Server 2008 R2 up to Windows 10/11.

## Quick Build
Run `build.bat` or compile via Command Prompt:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /r:System.Management.dll /r:System.Drawing.dll SilentDisk.cs
