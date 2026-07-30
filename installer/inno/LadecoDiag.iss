#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installer"
#endif

[Setup]
AppId={{D73EA212-5E87-48A6-8A99-E372FDD4B935}
AppName=Ladeco IT Diagnostic Tool
AppVersion={#MyAppVersion}
AppPublisher=Ladeco IT
DefaultDirName={autopf}\Ladeco IT\Ladeco Diag
DefaultGroupName=Ladeco IT
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=LadecoDiagSetup_{#MyAppVersion}
UninstallDisplayIcon={app}\LadecoDiag.exe
SetupIconFile=..\..\src\Ladeco.Diag.App\Resources\Images\Logo.ico

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Ladeco IT\Ladeco Diag"; Filename: "{app}\LadecoDiag.exe"
Name: "{autodesktop}\Ladeco Diag"; Filename: "{app}\LadecoDiag.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LadecoDiag.exe"; Description: "Launch Ladeco Diag"; Flags: nowait postinstall skipifsilent
