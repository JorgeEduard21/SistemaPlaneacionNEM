; Script de Inno Setup para el Sistema de Planeacion NEM
; Genera un instalador profesional, autocontenido y en espanol para docentes.

#define MyAppName "Sistema de Planeacion NEM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Sistema de Planeacion Docente NEM"
#define MyAppExeName "Frontend.exe"
#define MyAppId "{{B72E9D42-0F92-4F7D-A7F1-356C427B910A}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SistemaPlaneacionNEM
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=dist\installer
OutputBaseFilename=Instalador_Planeacion_NEM_v1.0.0
SetupIconFile=Frontend\Logo PLANEACION.ico
UninstallDisplayIcon={app}\Logo PLANEACION.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "dist\staging\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Frontend\Logo PLANEACION.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Logo PLANEACION.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Logo PLANEACION.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
