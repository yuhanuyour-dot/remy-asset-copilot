param(
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string]$RuntimeSource,
    [string]$Python = 'python',
    [ValidateSet('Standard','Full','Update')][string[]]$Variants = @('Standard','Full','Update')
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$work=Join-Path $root 'work'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$env:DOTNET_CLI_HOME=Join-Path $work 'dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:TEMP=$work
$env:TMP=$work
if(-not $RuntimeSource){$RuntimeSource=Join-Path $root 'runtime'}
if(-not(Test-Path -LiteralPath $Iscc)){throw 'Install Inno Setup 6.7.3, or pass -Iscc with the path to ISCC.exe.'}
if(-not(Test-Path -LiteralPath (Join-Path $RuntimeSource 'node.exe'))){throw 'Pass -RuntimeSource with the runtime folder from a Full installation.'}
if($Variants -contains 'Full' -and -not(Test-Path -LiteralPath (Join-Path $RuntimeSource 'vision/model/onnx/model_quantized.onnx'))){throw 'Full requires the local image-size model in RuntimeSource/vision.'}
& dotnet build (Join-Path $root 'AssetCopilot/AssetCopilot.csproj') -c Release -f net48 -o (Join-Path $root 'AssetCopilot/dist-universal') --nologo
if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}
$payload=Join-Path $root ('work/installer-payload-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
& $Python (Join-Path $PSScriptRoot 'stage.py') --root $root --output $payload --runtime $RuntimeSource
if($LASTEXITCODE -ne 0){throw 'Staging failed'}
foreach($variant in $Variants){
    & $Iscc /Q "/DPayload=$payload" "/DVariant=$variant" (Join-Path $PSScriptRoot 'RemyAssetCopilot.iss')
    if($LASTEXITCODE -ne 0){throw "Installer build failed: $variant"}
}
Get-ChildItem -LiteralPath (Join-Path $root 'releases') -Filter 'RemyAssetCopilot-Setup-0.5.3-*.exe' | Where-Object { $_.Name -notlike '*-QA.exe' } | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"} | Set-Content -LiteralPath (Join-Path $root 'releases/SHA256SUMS-0.5.3.txt') -Encoding ascii
