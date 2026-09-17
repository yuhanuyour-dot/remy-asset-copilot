$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../RhinoRuntime.ps1')
$checks=0
foreach($minor in @(0,1,11,12,19,20,35)){
    foreach($mask in 0..3){
        $runtimes=@();if($mask -band 1){$runtimes+=7};if($mask -band 2){$runtimes+=8}
        $expected=$null
        if($minor -lt 12){if($mask -band 1){$expected='/netcore'}}
        elseif($mask -band 2){$expected='/netcore-8'}
        elseif($mask -band 1){$expected='/netcore-7'}
        $actual=$null;$rejected=$false
        try{$actual=Get-RemyRuntimeArgument -RhinoVersion ([version]"8.$minor.0.0") -InstalledRuntimes $runtimes}catch{$rejected=$true}
        if($expected){if($rejected -or $actual -ne $expected){throw "Wrong runtime for Rhino 8.$minor / mask $mask"}}
        elseif(-not $rejected){throw "Missing runtime accepted: 8.$minor / mask $mask"}
        $checks++
    }
}
foreach($version in @('7.35.0.0','9.0.0.0')){
    $rejected=$false
    try{Get-RemyRuntimeArgument -RhinoVersion ([version]$version) -InstalledRuntimes @(7,8) | Out-Null}catch{$rejected=$true}
    if(-not $rejected){throw "Unsupported Rhino accepted: $version"};$checks++
}
Write-Output "$checks runtime compatibility checks passed."
