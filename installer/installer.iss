; Inno Setup script for DCSMissionInspector

#define AppName "DCSMissionInspector"
#define AppVersion "0.0.0"
#define PublishDir "..\publish"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\DCSMissionInspector
DefaultGroupName=DCSMissionInspector
PrivilegesRequired=admin
OutputBaseFilename=DCSMissionInspector-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "installer\appsettings.json"; DestDir: "{userappdata}\DCSMissionInspector"; Flags: ignoreversion

[Icons]
Name: "{group}\DCSMissionInspector"; Filename: "{app}\DCSMissionInspector.exe"
Name: "{commondesktop}\DCSMissionInspector"; Filename: "{app}\DCSMissionInspector.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    ForceDirectories(ExpandConstant('{userappdata}\DCSMissionInspector'));
end;
