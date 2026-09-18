#define Version "0.5.4"
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
Name: "en"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation"
Name: "custom"; Description: "Custom installation"; Flags: iscustom
[Components]
Name: "core"; Description: "Rhino plugin and GLB decoder"; Types: full custom; Flags: fixed
#if Variant == "Full"
Name: "vision"; Description: "Offline image size estimates"; Types: full
#endif
[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; Flags: unchecked

[Files]
Source: "{#Payload}\AssetCopilot\*"; DestDir: "{app}\AssetCopilot"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core
Source: "{#Payload}\Start-AssetCopilot.*"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "{#Payload}\Install-Update.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "{#Payload}\UserGuide.txt"; DestDir: "{app}"; Flags: ignoreversion; Components: core
#if Variant != "Update"
Source: "{#Payload}\runtime\node.exe"; DestDir: "{app}\runtime"; Flags: ignoreversion; Components: core
Source: "{#Payload}\runtime\NODE-LICENSE.txt"; DestDir: "{app}\runtime"; Flags: ignoreversion; Components: core
#endif
#if Variant == "Full"
Source: "{#Payload}\runtime\vision\*"; DestDir: "{app}\runtime\vision"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: vision
#endif

[Icons]
Name: "{group}\Remy Asset Copilot"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\Start-AssetCopilot.vbs"""; WorkingDir: "{app}"; IconFilename: "{code:RhinoExecutable}"
Name: "{group}\User Guide"; Filename: "{app}\UserGuide.txt"
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
  RhinoPage := CreateInputFilePage(wpSelectDir, 'Locate Rhino',
    'Choose your Rhino 8 executable',
    'Supports Windows x64, Rhino 8.0 onward (8.x), .NET Framework and .NET 7/8. No Rhino runtime changes needed.');
  RhinoPage.Add('Rhino.exe:', 'Rhino executable|Rhino.exe', '.exe');
  RhinoPage.Values[0] := ExpandConstant('{param:RHINOEXE|' + DetectRhino + '}');
  DataPage := CreateInputDirPage(RhinoPage.ID, 'Models and data',
    'Choose where models, textures and cache are saved',
    'Uninstall preserves this directory. Upgrades keep the existing data location.', False, '');
  DataPage.Add('Data directory:');
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
    Result := 'Save your work and close all Rhino windows before retrying. Setup will not terminate Rhino.'; Exit;
  end;
  if not GetVersionNumbersString(RhinoPage.Values[0], V) then begin
    Result := 'Select a valid Rhino.exe.'; Exit;
  end;
  if (CompareText(ExtractFileName(RhinoPage.Values[0]), 'Rhino.exe') <> 0) or
     not GetVersionComponents(RhinoPage.Values[0], Major, Minor, Build, Revision) or (Major <> 8) then begin
    Result := 'Select Rhino 8.0 or a later Rhino 8 release.'; Exit;
  end;
  Marker := AddBackslash(WizardDirValue) + 'remy-install.ini';
  Existing := GetIniString('Storage', 'DataRoot', '', Marker);
  if (Existing = '') and FileExists(AddBackslash(WizardDirValue) + 'settings.json') and
     FileExists(AddBackslash(WizardDirValue) + 'runtime\node.exe') then Existing := WizardDirValue;
  if Existing <> '' then DataPage.Values[0] := Existing;
  if not AbsoluteFolder(DataPage.Values[0]) then begin
    Result := 'Choose an absolute folder on a local drive, for example E:\RemyData.'; Exit;
  end;
#if Variant == "Update"
  if not FileExists(Marker) or not FileExists(AddBackslash(WizardDirValue) + 'runtime\node.exe') or
     not FileExists(AddBackslash(WizardDirValue) + 'AssetCopilot\dist\AssetCopilot.rhp') then begin
    Result := 'This update requires an installed universal release. Use Standard or Full for first installation or 0.4.x migration.'; Exit;
  end;
#endif
  if FileExists(AddBackslash(WizardDirValue) + 'AssetCopilot\dist\AssetCopilot.dll') then begin
    Result := 'A duplicate AssetCopilot\dist\AssetCopilot.dll exists. Back it up outside this folder and retry.'; Exit;
  end;
  if not ForceDirectories(DataPage.Values[0]) then begin
    Result := 'Cannot create the data directory. Choose a writable folder.'; Exit;
  end;
  Probe := AddBackslash(DataPage.Values[0]) + '.remy-install-write-test-' + GetDateTimeString('yyyymmddhhnnsszzz', '-', ':');
  if not SaveStringToFile(Probe, 'Remy installer permission test', False) then begin
    Result := 'The data directory is not writable. Choose another location.'; Exit;
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
var Marker, Backup, LegacyDependencies, LegacyGuide, LegacyShortcut: String;
begin
  if (CurStep = ssInstall) and FileExists(ExpandConstant('{app}\AssetCopilot\dist\AssetCopilot.rhp')) then begin
    Backup := AddBackslash(DataPage.Values[0]) + 'backups\install-' + GetDateTimeString('yyyymmdd-hhnnss-zzz', '-', ':');
    BackupProgramTree(ExpandConstant('{app}\AssetCopilot\dist'), Backup + '\dist');
    if FileExists(ExpandConstant('{app}\remy-install.ini')) then
      FileCopy(ExpandConstant('{app}\remy-install.ini'), Backup + '\remy-install.ini', False);
    SaveStringToFile(Backup + '\previous-plugin-path.txt', OldPlugin, False);
    // 0.5.1 shipped a Core-only dependency manifest. The universal assembly
    // resolves the adjacent Framework-compatible dependencies on all runtimes.
    // Remove only this obsolete installer-owned file, after backing it up.
    LegacyDependencies := ExpandConstant('{app}\AssetCopilot\dist\AssetCopilot.deps.json');
    if FileExists(LegacyDependencies) and not DeleteFile(LegacyDependencies) then
      RaiseException('Could not remove the backed-up legacy dependency manifest.');
  end;
  if CurStep = ssPostInstall then begin
    Marker := ExpandConstant('{app}\remy-install.ini');
    if not FileExists(Marker) then SaveStringToFile(Marker, #255#254, False);
    if not SetIniString('Storage', 'DataRoot', DataPage.Values[0], Marker) or
       not SetIniString('Rhino', 'Exe', RhinoPage.Values[0], Marker) or
       not SetIniString('Install', 'Version', '{#Version}', Marker) then
      RaiseException('Unable to save install configuration.');
    // Keep an old installer-owned guide link useful, but remove its old menu label.
    LegacyGuide := ExpandConstant('{app}\使用说明.txt');
    if FileExists(LegacyGuide) then
      FileCopy(ExpandConstant('{app}\UserGuide.txt'), LegacyGuide, False);
    LegacyShortcut := ExpandConstant('{group}\使用说明.lnk');
    if FileExists(LegacyShortcut) then DeleteFile(LegacyShortcut);
    // Settings and task records are never copied from the author or overwritten.
    ForceDirectories(AddBackslash(DataPage.Values[0]) + 'models');
    ForceDirectories(AddBackslash(DataPage.Values[0]) + 'work');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := not RhinoIsRunning;
  if not Result then MsgBox('Save and close Rhino before uninstalling.', mbError, MB_OK);
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
