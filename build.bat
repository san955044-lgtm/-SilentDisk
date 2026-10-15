@echo off
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /r:System.Management.dll /r:System.Drawing.dll SilentDisk.cs
pause
