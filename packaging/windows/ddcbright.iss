; Inno Setup script for DdcBright.
;
; Builds a per-user installer (no admin/UAC prompt) that installs the
; self-contained single-file exe already produced by `dotnet publish`,
; adds Start Menu + optional Desktop shortcuts, and registers a proper
; uninstaller in Add/Remove Programs. The app registers its own autostart
; Run key on every launch (see Autostart.cs); the uninstaller runs
; `ddcbright.exe --unregister-autostart` before removing the exe so that
; key doesn't outlive it.
;
; Build locally:
;   dotnet publish ..\..\windows\DdcBright\DdcBright.csproj -c Release -r win-x64 -o ..\..\windows\DdcBright\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
;   iscc ddcbright.iss
;
; Build with an explicit publish output + version (what CI does):
;   iscc /DSourceExe="..\..\publish-out\ddcbright.exe" /DMyAppVersion="1.2.3" ddcbright.iss

#define MyAppName "DdcBright"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#define MyAppPublisher "mvrck19"
#define MyAppURL "https://github.com/mvrck19/ddcbright"
#define MyAppExeName "ddcbright.exe"
#ifndef SourceExe
  #define SourceExe "..\..\windows\DdcBright\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\ddcbright.exe"
#endif

[Setup]
; Fixed forever: identifies this app across versions so upgrades/uninstalls
; track correctly in Add/Remove Programs. Do not regenerate.
AppId={{BA174CD1-BDA9-4203-8AE8-A9DFB3C0E581}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
; Per-user install: no admin prompt, matches the app's own no-fuss,
; run-it-and-go distribution model. Installs under the user's own
; LocalAppData rather than Program Files.
DefaultDirName={localappdata}\Programs\DdcBright
DefaultGroupName=DdcBright
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=ddcbright-setup
SetupIconFile=..\..\windows\DdcBright\sun.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Matches the named mutex the app itself creates while running (see
; App.xaml.cs) -- lets Setup detect and prompt to close a running instance
; before installing or uninstalling over it.
AppMutex=DdcBright-SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DdcBright"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall DdcBright"; Filename: "{uninstallexe}"
Name: "{userdesktop}\DdcBright"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch DdcBright"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Best-effort; the app's own Unregister() already swallows failures. Removes
; the HKCU Run entry the app wrote on first launch, so it doesn't outlive
; the exe it points at.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-autostart"; Flags: waituntilterminated runhidden; RunOnceId: "UnregisterAutostart"
