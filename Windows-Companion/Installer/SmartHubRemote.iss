#define MyAppName "SmartHub Remote Companion"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Smart Hub Technology"
#define MyAppExeName "SmartHubRemote.exe"

[Setup]
AppId={{98F3A03E-9B9D-4F77-92DF-B320B93A44D0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SmartHub Remote
DefaultGroupName=SmartHub Remote
OutputDir=Output
OutputBaseFilename=SmartHubRemote_Companion_Setup_v1.1.0
SetupIconFile=..\SmartHubRemote\Assets\logo.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern

[Files]
Source: "..\SmartHubRemote\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SmartHub Remote"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SmartHub Remote"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SmartHub Remote"; Flags: nowait postinstall skipifsilent
