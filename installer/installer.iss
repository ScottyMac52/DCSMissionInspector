; Reusable Inno Setup script for DCSMissionInspector apps
; Pass these via /D switches from the workflow:
;   /DAppName=xxx
;   /DAppVersion=1.2.3
;   /DPublishDir=publish\AppName
;   /DAppPublisher=ScottyMac52

#define AppName "MyApp"
#define AppVersion "1.0.0"
#define PublishDir "..\publish\MyApp"
#define AppPublisher "ScottyMac52"

[Setup]
AppName={#AppName}
AppPublisher={#AppPublisher}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=admin
OutputDir=..\_setup
OutputBaseFilename={#AppName}-Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppName}.exe"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    ForceDirectories(ExpandConstant('{userappdata}\{#AppName}'));
end;