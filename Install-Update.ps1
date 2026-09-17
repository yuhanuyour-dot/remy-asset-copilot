param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
# Compatibility entry point. Installation and updates now belong to the signed-aware setup wizard.
if($CheckOnly){& "$PSScriptRoot/Start-AssetCopilot.ps1" -CheckOnly;exit $LASTEXITCODE}
Write-Output 'Please run RemyAssetCopilot-Setup-0.5.1-Standard.exe (or Full.exe) to install/update.'
exit 0
