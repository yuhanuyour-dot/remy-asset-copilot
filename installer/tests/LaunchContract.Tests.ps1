param([string]$InstallRoot=(Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference='Stop'
$capture=New-Object System.Collections.Generic.List[object]
# Mock only process launch and plugin registration; never start/stop Rhino during this check.
function Get-Process { param($Name,$ErrorAction) return $null }
function Get-ItemProperty { param($LiteralPath,$ErrorAction) [pscustomobject]@{FileName=(Join-Path $InstallRoot 'AssetCopilot\dist\AssetCopilot.rhp')} }
function Start-Process { param($FilePath,$ArgumentList,$WorkingDirectory,$WindowStyle)
    $capture.Add([pscustomobject]@{FilePath=$FilePath;Arguments=$ArgumentList;WorkingDirectory=$WorkingDirectory})
}
& (Join-Path $InstallRoot 'Start-AssetCopilot.ps1')
if($capture.Count -eq 0){throw 'Launcher did not request Rhino startup.'}
if($capture[0].Arguments -ne '/runscript="_AssetCopilot"'){throw 'Launcher must keep the user runtime and invoke only AssetCopilot.'}
if($capture[0].WorkingDirectory -ne $InstallRoot){throw 'Incorrect install directory.'}
Write-Output 'PASS: normal Rhino command launch preserves runtime preferences.'

