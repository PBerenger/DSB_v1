#define AppName "Double Sceens Black"
#define AppVersion "1.0.0"

[Setup]
AppId={{B1E2A2F8-1A10-4F99-8C3A-7C6A7FB7B5D2}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Double Sceens Black
DefaultDirName={autopf}\DoubleSceensBlack
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\DoubleSceensBlack.exe
SetupIconFile=Logo_BSD.ico
OutputDir=.
OutputBaseFilename=BSD_installer
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis supplémentaires :"; Flags: unchecked

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\DoubleSceensBlack.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DoubleSceensBlack.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DoubleSceensBlack.exe"; Description: "Lancer {#AppName}"; Flags: postinstall nowait skipifsilent runasoriginaluser
