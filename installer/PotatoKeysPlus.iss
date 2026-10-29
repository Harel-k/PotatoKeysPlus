; Potato Keys+ installer (Inno Setup 6)
; Built by tools\build-release.ps1, which passes the version and publish folder:
;   ISCC.exe /DAppVersion=0.2.0 /DPublishDir=..\publish\win-x64 installer\PotatoKeysPlus.iss

#define AppName "Potato Keys+"
#define AppExe "PotatoKeysPlus.exe"
#define AppPublisher "Harel"
#define AppUrl "https://github.com/Harel-k/PotatoKeysPlus"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish\win-x64"
#endif

[Setup]
; Never change AppId: it's how Windows knows new versions replace old ones.
AppId={{6B1E4C2A-9F3D-4B7E-A8C5-2D9F7E31B0A4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup

; Per-user install by default: no admin prompt, and the auto-updater can update silently.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\Potato Keys+
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

OutputDir=..\dist
OutputBaseFilename=PotatoKeysPlus-Setup-{#AppVersion}
SetupIconFile=..\Assets\PotatoKeysPlus.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE

WizardStyle=modern
DisableWelcomePage=no
WizardImageFile=wizard-large.bmp,wizard-large-200.bmp
WizardSmallImageFile=wizard-small.bmp,wizard-small-200.bmp

Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nMove the mouse, click and scroll with just your keyboard, make your own hotkeys, and pin it on top of everything.%n%nIt's a good idea to close other apps before continuing.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start Potato Keys+ when &Windows starts (in the tray)"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Only set during a normal (wizard) install. Silent auto-updates leave the user's in-app choice alone.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PotatoKeysPlus"; \
  ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: startup; Check: not WizardSilent; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent
; After a silent auto-update, start the new version again.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: WizardSilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "StopPotatoKeys"

[Code]
// Ask whether to also remove saved settings and hotkeys.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SettingsDir := ExpandConstant('{userappdata}\PotatoKeysPlus');
    if DirExists(SettingsDir) and not UninstallSilent then
      if MsgBox('Also delete your Potato Keys+ settings and hotkeys?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(SettingsDir, True, True, True);
  end;
end;
