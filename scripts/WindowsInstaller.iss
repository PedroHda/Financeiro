#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
[Setup]
AppId={{06CDF880-6977-4C8F-9BFA-82C74D176738}
AppName=Financeiro Pessoal
AppVersion={#AppVersion}
AppPublisher=Pedro Domiciano
DefaultDirName={localappdata}\Programs\Financeiro
DefaultGroupName=Financeiro
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDirectory}
OutputBaseFilename=Financeiro-Setup-x64-{#AppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\FinanceiroApp.exe
WizardStyle=modern
CloseApplications=yes
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; Flags: unchecked
[Files]
Source: "{#SourceDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Financeiro"; Filename: "{app}\FinanceiroApp.exe"
Name: "{autodesktop}\Financeiro"; Filename: "{app}\FinanceiroApp.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\FinanceiroApp.exe"; Description: "Abrir Financeiro"; Flags: nowait postinstall skipifsilent
