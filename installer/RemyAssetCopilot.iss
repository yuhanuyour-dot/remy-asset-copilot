#define Version "0.5.1"
#ifndef Variant
  #define Variant "Standard"
#endif
#ifndef Payload
  #define Payload "..\work\installer-payload"
#endif
#ifdef QA
  #define Identity "RemyAssetCopilot.InstallerQA"
  #define Product "Remy Installer QA"
  #define RegPlugin "Software\RemyAssetCopilot\InstallerQA\RhinoPlugin"
  #define Suffix "-QA"
#else
  #define Identity "RemyAssetCopilot.Windows"
  #define Product "Remy Asset Copilot"
  #define RegPlugin "Software\McNeel\Rhinoceros\8.0\Plug-ins\1f31b7d3-758a-43ef-8d03-6ea3be43eec7"
  #define Suffix ""
#endif
[Setup]
AppId={#Identity}
AppName={#Product}
AppVersion={#Version}
AppPublisher=Remy Asset Copilot
VersionInfoVersion={#Version}.0
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName={#Product}
UninstallDisplayName={#Product}
UninstallDisplayIcon={app}\AssetCopilot\dist\AssetCopilot.rhp
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\releases
OutputBaseFilename=RemyAssetCopilot-Setup-{#Version}-{#Variant}{#Suffix}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=no
CloseApplications=no
RestartApplications=no
UninstallFilesDir={app}\uninstall
SetupLogging=yes
SetupMutex={#Identity}.Setup
ChangesAssociations=no

[Languages]
Name: "zhcn"; MessagesFile: "ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "完整安装 / Full"
Name: "custom"; Description: "自定义 / Custom"; Flags: iscustom
[Components]
Name: "core"; Description: "Rhino 插件与 GLB 解码 / Plugin and GLB decoder"; Types: full custom; Flags: fixed
#if Variant == "Full"
Name: "vision"; Description: "本地图片尺寸识别 / Offline image size estimates"; Types: full
#endif
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式 / Create desktop shortcut"; Flags: unchecked

[Files]
Source: "{#Payload}\AssetCopilot\*"; DestDir: "{app}\AssetCopilot"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core
Source: "{#Payload}\Start-AssetCopilot.*"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "{#Payload}\RhinoRuntime.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "{#Payload}\Install-Update.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "{#Payload}\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion; Components: core
#if Variant != "Update"
Source: "{#Payload}\runtime\node.exe"; DestDir: "{app}\runtime"; Flags: ignoreversion; Components: core
Source: "{#Payload}\runtime\NODE-LICENSE.txt"; DestDir: "{app}\runtime"; Flags: ignoreversion; Components: core
#endif
#if Variant == "Full"
Source: "{#Payload}\runtime\vision\*"; DestDir: "{app}\runtime\vision"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: vision
#endif

[Icons]
Name: "{group}\Remy Asset Copilot"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\Start-AssetCopilot.vbs"""; WorkingDir: "{app}"; IconFilename: "{code:RhinoExecutable}"
Name: "{group}\使用说明"; Filename: "{app}\使用说明.txt"
Name: "{autodesktop}\Remy Asset Copilot"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\Start-AssetCopilot.vbs"""; WorkingDir: "{app}"; IconFilename: "{code:RhinoExecutable}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "{#RegPlugin}"; ValueType: string; ValueName: "Name"; ValueData: "AssetCopilot"
Root: HKCU; Subkey: "{#RegPlugin}"; ValueType: dword; ValueName: "LoadMode"; ValueData: "2"
Root: HKCU; Subkey: "{#RegPlugin}\PlugIn"; ValueType: string; ValueName: "FileName"; ValueData: "{app}\AssetCopilot\dist\AssetCopilot.rhp"

[Code]
var
  DataPage: TInputDirWizardPage;
  RhinoPage: TInputFileWizardPage;
  OldPlugin: String;

function T(Chinese, English: String): String;
begin
  if ActiveLanguage = 'zhcn' then Result := Chinese else Result := English;
end;

function DefaultInstallDir(Param: String): String;
var P, Candidate: String;
begin
  Result := ExpandConstant('{localappdata}\Programs\RemyAssetCopilot{#Suffix}');
  if RegQueryStringValue(HKCU, '{#RegPlugin}\PlugIn', 'FileName', P) then begin
    Candidate := ExtractFileDir(ExtractFileDir(ExtractFileDir(P)));
    if FileExists(Candidate + '\AssetCopilot\dist\AssetCopilot.rhp') and
       FileExists(Candidate + '\runtime\node.exe') then Result := Candidate;
  end;
end;

function DetectRhino: String;
var P: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM64, 'SOFTWARE\McNeel\Rhinoceros\8.0\Install', 'Path', P) then
    Result := AddBackslash(P) + 'Rhino.exe';
  if not FileExists(Result) then Result := ExpandConstant('{pf64}\Rhino 8\System\Rhino.exe');
end;

function RhinoExecutable(Param: String): String;
begin Result := RhinoPage.Values[0]; end;

function DesktopRuntimeExists(RuntimeMajor: String): Boolean;
var Found: TFindRec; DotnetRoot, RegistryRoot: String;
begin
  DotnetRoot := ExpandConstant('{pf64}\dotnet');
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', RegistryRoot) then
    if RegistryRoot <> '' then DotnetRoot := RegistryRoot;
  Result := FindFirst(AddBackslash(DotnetRoot) + 'shared\Microsoft.WindowsDesktop.App\' + RuntimeMajor + '.*', Found);
  if Result then FindClose(Found);
end;

function ChooseRuntimeArgument(Major, Minor: Word; Has7, Has8: Boolean): String;
begin
  Result := '';
  if Major <> 8 then Exit;
  if Minor < 12 then begin
    if Has7 then Result := '/netcore';
  end else if Has8 then Result := '/netcore-8'
  else if Has7 then Result := '/netcore-7';
end;

#ifdef QA
procedure CheckRuntimePolicy;
begin
  if (ChooseRuntimeArgument(8, 0, True, False) <> '/netcore') or
     (ChooseRuntimeArgument(8, 11, True, True) <> '/netcore') or
     (ChooseRuntimeArgument(8, 0, False, True) <> '') or
     (ChooseRuntimeArgument(8, 12, True, True) <> '/netcore-8') or
     (ChooseRuntimeArgument(8, 19, True, False) <> '/netcore-7') or
     (ChooseRuntimeArgument(8, 20, False, True) <> '/netcore-8') or
     (ChooseRuntimeArgument(8, 35, False, False) <> '') or
     (ChooseRuntimeArgument(7, 35, True, True) <> '') or
     (ChooseRuntimeArgument(9, 0, True, True) <> '') then
       RaiseException('Compatibility policy test failed.');
  Log('PASS: Rhino 8.0+ compatibility policy (9 cases).');
end;
#endif

function RhinoIsRunning: Boolean;
var Locator, Services, Processes: Variant;
begin
  // Fail closed if process enumeration is unavailable. Never kill Rhino.
  Result := True;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('', 'root\CIMV2');
    Processes := Services.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name="Rhino.exe"');
    Result := Processes.Count > 0;
  except
    Log('Could not check Rhino processes.');
  end;
end;

procedure InitializeWizard;
begin
#ifdef QA
  CheckRuntimePolicy;
#endif
  RhinoPage := CreateInputFilePage(wpSelectDir, T('查找 Rhino', 'Locate Rhino'),
    T('选择 Rhino 8 的程序位置', 'Choose your Rhino 8 executable'),
    T('支持 Windows x64、Rhino 8.0 起的全部 Rhino 8 小版本；启动时选择兼容的 .NET 运行时。', 'Supports Windows x64 and Rhino 8.0 onward (8.x). Uses a compatible .NET 7 or 8 runtime.'));
  RhinoPage.Add('Rhino.exe:', 'Rhino executable|Rhino.exe', '.exe');
  RhinoPage.Values[0] := ExpandConstant('{param:RHINOEXE|' + DetectRhino + '}');
  DataPage := CreateInputDirPage(RhinoPage.ID, T('模型与数据目录', 'Models and data'),
    T('选择模型、贴图和缓存的保存位置', 'Choose where models, textures and cache are saved'),
    T('卸载程序时保留此目录。已有安装将沿用原数据目录。', 'Uninstall preserves this directory. Upgrades keep the existing data location.'), False, '');
  DataPage.Add(T('数据目录：', 'Data directory:'));
  DataPage.Values[0] := ExpandConstant('{param:DATADIR|{localappdata}\RemyAssetCopilot{#Suffix}\Data}');
end;

procedure CurPageChanged(CurPageID: Integer);
var Marker, Existing: String;
begin
  if CurPageID = DataPage.ID then begin
    Marker := AddBackslash(WizardDirValue) + 'remy-install.ini';
    Existing := GetIniString('Storage', 'DataRoot', '', Marker);
    if (Existing = '') and FileExists(AddBackslash(WizardDirValue) + 'settings.json') and
       FileExists(AddBackslash(WizardDirValue) + 'runtime\node.exe') then Existing := WizardDirValue;
    if Existing <> '' then DataPage.Values[0] := Existing;
    DataPage.Edits[0].Enabled := Existing = '';
    DataPage.Buttons[0].Enabled := Existing = '';
  end;
end;

function AbsoluteFolder(P: String): Boolean;
begin
  // Local fixed-drive paths are supported; mapped/network folders are intentionally not installer targets.
  Result := (Length(P) > 3) and (P[2] = ':') and (P[3] = '\') and (Pos(#13, P) = 0) and (Pos(#10, P) = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Major, Minor, Build, Revision: Word; Marker, Existing, Probe, V: String;
begin
  Result := '';
  if RhinoIsRunning then begin
    Result := T('请先保存项目并关闭所有 Rhino 窗口，然后重试。安装器不会结束 Rhino 进程。', 'Save your work and close all Rhino windows before retrying. Setup will not terminate Rhino.'); Exit;
  end;
  if not GetVersionNumbersString(RhinoPage.Values[0], V) then begin
    Result := T('请选择有效的 Rhino.exe。', 'Select a valid Rhino.exe.'); Exit;
  end;
  if (CompareText(ExtractFileName(RhinoPage.Values[0]), 'Rhino.exe') <> 0) or
     not GetVersionComponents(RhinoPage.Values[0], Major, Minor, Build, Revision) or (Major <> 8) then begin
    Result := T('请选择 Rhino 8.0 或之后的 Rhino 8。', 'Select Rhino 8.0 or a later Rhino 8 release.'); Exit;
  end;
  if ChooseRuntimeArgument(Major, Minor, DesktopRuntimeExists('7'), DesktopRuntimeExists('8')) = '' then begin
    Result := T('未找到兼容的 .NET 桌面运行时。Rhino 8.0–8.11 需要 .NET 7；8.12+ 可用 .NET 7 或 8。请修复 Rhino 安装后重试。', 'Compatible Desktop Runtime missing: Rhino 8.0-8.11 needs .NET 7; 8.12+ supports .NET 7 or 8. Repair Rhino and retry.'); Exit;
  end;
  Marker := AddBackslash(WizardDirValue) + 'remy-install.ini';
  Existing := GetIniString('Storage', 'DataRoot', '', Marker);
  if (Existing = '') and FileExists(AddBackslash(WizardDirValue) + 'settings.json') and
     FileExists(AddBackslash(WizardDirValue) + 'runtime\node.exe') then Existing := WizardDirValue;
  if Existing <> '' then DataPage.Values[0] := Existing;
  if not AbsoluteFolder(DataPage.Values[0]) then begin
    Result := T('请选择本机磁盘上的完整数据目录，例如 E:\RemyData。', 'Choose an absolute folder on a local drive, for example E:\RemyData.'); Exit;
  end;
#if Variant == "Update"
  if not FileExists(Marker) or not FileExists(AddBackslash(WizardDirValue) + 'runtime\node.exe') or
     not FileExists(AddBackslash(WizardDirValue) + 'AssetCopilot\dist\AssetCopilot.rhp') then begin
    Result := T('更新包需要已安装 0.5.0 通用版。首次安装或从 0.4.x 升级请使用 Standard 或 Full。', 'This update requires an installed universal release. Use Standard or Full for first installation or 0.4.x migration.'); Exit;
  end;
#endif
  if FileExists(AddBackslash(WizardDirValue) + 'AssetCopilot\dist\AssetCopilot.dll') then begin
    Result := T('发现旧版重复 DLL。请先将 AssetCopilot\dist\AssetCopilot.dll 备份到其他目录，然后重试。', 'A duplicate AssetCopilot\dist\AssetCopilot.dll exists. Back it up outside this folder and retry.'); Exit;
  end;
  if not ForceDirectories(DataPage.Values[0]) then begin
    Result := T('无法创建数据目录，请选择有写入权限的位置。', 'Cannot create the data directory. Choose a writable folder.'); Exit;
  end;
  Probe := AddBackslash(DataPage.Values[0]) + '.remy-install-write-test-' + GetDateTimeString('yyyymmddhhnnsszzz', '-', ':');
  if not SaveStringToFile(Probe, 'Remy installer permission test', False) then begin
    Result := T('数据目录不可写，请选择其他位置。', 'The data directory is not writable. Choose another location.'); Exit;
  end;
  DeleteFile(Probe);
  OldPlugin := '';
  RegQueryStringValue(HKCU, '{#RegPlugin}\PlugIn', 'FileName', OldPlugin);
end;

procedure BackupProgramTree(Source, Destination: String);
var F: TFindRec; FromFile, ToFile: String;
begin
  if not ForceDirectories(Destination) then RaiseException('Could not create upgrade backup.');
  if FindFirst(AddBackslash(Source) + '*', F) then begin
    try
      repeat
        if (F.Name <> '.') and (F.Name <> '..') then begin
          FromFile := AddBackslash(Source) + F.Name;
          ToFile := AddBackslash(Destination) + F.Name;
          if F.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
            BackupProgramTree(FromFile, ToFile)
          else if not FileCopy(FromFile, ToFile, False) then
            RaiseException('Could not back up: ' + FromFile);
        end;
      until not FindNext(F);
    finally FindClose(F); end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Marker, Backup: String;
begin
  if (CurStep = ssInstall) and FileExists(ExpandConstant('{app}\AssetCopilot\dist\AssetCopilot.rhp')) then begin
    Backup := AddBackslash(DataPage.Values[0]) + 'backups\install-' + GetDateTimeString('yyyymmdd-hhnnss-zzz', '-', ':');
    BackupProgramTree(ExpandConstant('{app}\AssetCopilot\dist'), Backup + '\dist');
    if FileExists(ExpandConstant('{app}\remy-install.ini')) then
      FileCopy(ExpandConstant('{app}\remy-install.ini'), Backup + '\remy-install.ini', False);
    SaveStringToFile(Backup + '\previous-plugin-path.txt', OldPlugin, False);
  end;
  if CurStep = ssPostInstall then begin
    Marker := ExpandConstant('{app}\remy-install.ini');
    if not FileExists(Marker) then SaveStringToFile(Marker, #255#254, False);
    if not SetIniString('Storage', 'DataRoot', DataPage.Values[0], Marker) or
       not SetIniString('Rhino', 'Exe', RhinoPage.Values[0], Marker) or
       not SetIniString('Install', 'Version', '{#Version}', Marker) then
      RaiseException('Unable to save install configuration.');
    // Settings and task records are never copied from the author or overwritten.
    ForceDirectories(AddBackslash(DataPage.Values[0]) + 'models');
    ForceDirectories(AddBackslash(DataPage.Values[0]) + 'work');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := not RhinoIsRunning;
  if not Result then MsgBox(T('请保存并关闭所有 Rhino 后再卸载。', 'Save and close Rhino before uninstalling.'), mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Current: String;
begin
  if CurUninstallStep = usUninstall then begin
    if RegQueryStringValue(HKCU, '{#RegPlugin}\PlugIn', 'FileName', Current) and
       (CompareText(Current, ExpandConstant('{app}\AssetCopilot\dist\AssetCopilot.rhp')) = 0) then
      RegDeleteKeyIncludingSubkeys(HKCU, '{#RegPlugin}');
    // Only our small install marker is removed. No recursive deletion of user folders.
    DeleteFile(ExpandConstant('{app}\remy-install.ini'));
  end;
end;
