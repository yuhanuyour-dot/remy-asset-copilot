Option Explicit
Dim fs, shell, script
Set fs = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
script = fs.BuildPath(fs.GetParentFolderName(WScript.ScriptFullName), "Start-AssetCopilot.ps1")
shell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & Chr(34) & script & Chr(34), 0, False
