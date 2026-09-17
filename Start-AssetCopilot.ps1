param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
try {
    $root=$PSScriptRoot
    $config=Join-Path $root 'remy-install.ini'
    $rhino=''
    if(Test-Path -LiteralPath $config){
        $section=''
        foreach($line in Get-Content -LiteralPath $config){
            if($line -match '^\[(.+)\]$'){$section=$Matches[1]}
            elseif($section -eq 'Rhino' -and $line -match '^Exe=(.+)$'){$rhino=$Matches[1]}
        }
    }
    if(-not $rhino -or -not(Test-Path -LiteralPath $rhino -ErrorAction SilentlyContinue)){
        $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
        try{$install=$base.OpenSubKey('SOFTWARE\McNeel\Rhinoceros\8.0\Install');if($install){$rhino=Join-Path ($install.GetValue('Path')) 'Rhino.exe';$install.Dispose()}}finally{$base.Dispose()}
    }
    if(-not $rhino -or -not(Test-Path -LiteralPath $rhino)){throw 'Rhino 8 was not found. Run the installer again and select Rhino.exe.'}
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo($rhino)
    $hostVersion=[version]::new($version.FileMajorPart,$version.FileMinorPart,$version.FileBuildPart,$version.FilePrivatePart)
    if($hostVersion.Major -ne 8){throw "Use Rhino 8.0 or a later Rhino 8 release."}
    $plugin=Join-Path $root 'AssetCopilot\dist\AssetCopilot.rhp'
    if(-not(Test-Path -LiteralPath $plugin)){throw 'The plugin file is missing. Run the installer again to repair it.'}
    $key='HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-ins\1f31b7d3-758a-43ef-8d03-6ea3be43eec7\PlugIn'
    $registered=(Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue).FileName
    if($registered -ne $plugin){throw 'Rhino has a different plugin location registered. Run the installer for this version to repair it.'}
    if($CheckOnly){Write-Output "Rhino: $rhino";Write-Output "Plugin: $plugin";Write-Output "Rhino version: $hostVersion / Runtime: Rhino current setting (Framework or Core)";exit 0}
    if(Get-Process -Name Rhino -ErrorAction SilentlyContinue){
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show('Rhino is already open. Type AssetCopilot in its command line to open the plugin.','Remy Asset Copilot')|Out-Null
        exit 0
    }
    # No script path on the command line: Unicode and spaces in installation paths are safe.
    Start-Process -FilePath $rhino -ArgumentList '/runscript="_AssetCopilot"' -WorkingDirectory $root -WindowStyle Normal | Out-Null
}catch{
    if($CheckOnly){Write-Error $_.Exception.Message;exit 1}
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message,'Remy Asset Copilot')|Out-Null
    exit 1
}
