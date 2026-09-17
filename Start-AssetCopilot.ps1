param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
try {
    $root=$PSScriptRoot
    . (Join-Path $root 'RhinoRuntime.ps1')
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
    if(-not $rhino -or -not(Test-Path -LiteralPath $rhino)){throw '未找到 Rhino 8，请重新运行安装程序，选择 Rhino.exe。'}
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo($rhino)
    $hostVersion=[version]::new($version.FileMajorPart,$version.FileMinorPart,$version.FileBuildPart,$version.FilePrivatePart)
    $runtimeArgument=Get-RemyRuntimeArgument -RhinoVersion $hostVersion -InstalledRuntimes @(Get-RemyInstalledRuntimes)
    $plugin=Join-Path $root 'AssetCopilot\dist\AssetCopilot.rhp'
    if(-not(Test-Path -LiteralPath $plugin)){throw '插件文件缺失，请重新运行安装程序修复。'}
    $key='HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-ins\1f31b7d3-758a-43ef-8d03-6ea3be43eec7\PlugIn'
    $registered=(Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue).FileName
    if($registered -ne $plugin){throw 'Rhino 当前注册的插件位置与此目录不同，请运行此版本的安装程序修复。'}
    if($CheckOnly){Write-Output "Rhino: $rhino";Write-Output "Plugin: $plugin";Write-Output "Rhino version: $hostVersion / Runtime: $runtimeArgument";exit 0}
    if(Get-Process -Name Rhino -ErrorAction SilentlyContinue){
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show('Rhino 已经打开。在 Rhino 命令行输入 AssetCopilot 即可打开插件。','Remy Asset Copilot')|Out-Null
        exit 0
    }
    # No script path on the command line: Unicode and spaces in installation paths are safe.
    Start-Process -FilePath $rhino -ArgumentList ($runtimeArgument+' /runscript="_AssetCopilot"') -WorkingDirectory $root -WindowStyle Normal | Out-Null
}catch{
    if($CheckOnly){Write-Error $_.Exception.Message;exit 1}
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message,'Remy Asset Copilot')|Out-Null
    exit 1
}
