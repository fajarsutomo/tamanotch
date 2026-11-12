; Inno Setup Script for TAMANOTCH
#define MyAppName "TAMANOTCH"
#ifndef MyAppVersion
#define MyAppVersion "1.7.0"
#endif
#define MyAppPublisher "TAMANOTCH"
#define MyAppURL "https://github.com/fajarsutomo/tamanotch"
#define MyAppExeName "TamaNotch.exe"

[Setup]
AppId={{E63BD772-BC81-426B-9989-138B426E778B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist\Installer
OutputBaseFilename=TAMANOTCH-Setup-{#MyAppVersion}
SetupIconFile=..\src\TamaNotch\Assets\app.ico
UninstallDisplayIcon={app}\app.ico
ChangesAssociations=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=*.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\TamaNotch\Assets\app.ico"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion

[InstallDelete]
; Nama exe berubah dari NotchMusic.exe ke TamaNotch.exe: buang sisa binari versi lama saat memperbarui
Type: files; Name: "{app}\NotchMusic.*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill process if running before uninstallation
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "StopTAMANOTCH"
Filename: "taskkill.exe"; Parameters: "/F /IM NotchMusic.exe"; Flags: runhidden; RunOnceId: "StopTAMANOTCHLegacy"

[UninstallDelete]
; Clean up app binaries (leaves user data in LocalAppData\TAMANOTCH untouched)
Type: filesandordirs; Name: "{app}"

[Registry]
; Clean up startup entry upon uninstall if user enabled it
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "TAMANOTCH"; Flags: dontcreatekey uninsdeletevalue

[Code]
procedure SHChangeNotify(wEventId: Integer; uFlags: Cardinal; dwItem1, dwItem2: Integer);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    try
      // Flush Windows Shell Icon and Association cache immediately
      SHChangeNotify($08000000, 0, 0, 0);
    except
    end;
  end;
end;
