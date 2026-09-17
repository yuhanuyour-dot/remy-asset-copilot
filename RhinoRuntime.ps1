# Pure compatibility policy is shared by the launcher and its tests.
function Get-RemyRuntimeArgument {
    param([version]$RhinoVersion, [int[]]$InstalledRuntimes)
    if($RhinoVersion.Major -ne 8){throw 'This release supports Rhino 8.0 and later Rhino 8 releases.'}
    if($RhinoVersion.Minor -lt 12){
        if($InstalledRuntimes -contains 7){return '/netcore'}
        throw 'Rhino 8.0-8.11 requires its .NET 7 Desktop Runtime. Repair the Rhino installation and retry.'
    }
    if($InstalledRuntimes -contains 8){return '/netcore-8'}
    if($InstalledRuntimes -contains 7){return '/netcore-7'}
    throw 'Rhino requires a .NET 7 or 8 Desktop Runtime (x64). Repair the Rhino installation and retry.'
}
function Get-RemyInstalledRuntimes {
    $roots=@("$env:ProgramFiles\dotnet")
    if($env:ProgramW6432){$roots+="$env:ProgramW6432\dotnet"}
    $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
    try{
        $install=$base.OpenSubKey('SOFTWARE\dotnet\Setup\InstalledVersions\x64')
        if($install){$value=$install.GetValue('InstallLocation');if($value){$roots+=$value};$install.Dispose()}
    }finally{$base.Dispose()}
    $found=@()
    foreach($root in ($roots | Select-Object -Unique)){
        foreach($major in @(7,8)){
            $framework=Join-Path $root 'shared\Microsoft.WindowsDesktop.App'
            $versions=Get-ChildItem -LiteralPath $framework -Directory -Filter "$major.*" -ErrorAction SilentlyContinue
            if($versions | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'PresentationFramework.dll')}){$found+=$major}
        }
    }
    $found | Select-Object -Unique
}
