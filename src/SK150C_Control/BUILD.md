# SK150C_Control Source

Single-file C# WinForms prototype.

Build on Windows with .NET Framework C# compiler:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /target:winexe /platform:anycpu /optimize+ /win32icon:'src\SK150C_Control\assets\app_icon.ico' /out:'release\SK150C_Control_v32.exe' /resource:src\SK150C_Control\assets\signature.jpg,SK150CControl.signature.jpg /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll 'src\SK150C_Control\Program.cs'
```
