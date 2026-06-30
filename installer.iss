[Setup]
AppName=Paste by Dan
AppVersion=1.0
AppPublisher=Dan
DefaultDirName={autopf}\PasteByDan
DefaultGroupName=Paste by Dan
OutputBaseFilename=PasteByDan-Setup
OutputDir=installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\PasteByDan.exe
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start automatically with Windows"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "PasteByDan\bin\Release\net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Paste by Dan"; Filename: "{app}\PasteByDan.exe"
Name: "{group}\Uninstall Paste by Dan"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Paste by Dan"; Filename: "{app}\PasteByDan.exe"; Tasks:

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PasteByDan"; ValueData: "{app}\PasteByDan.exe"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\PasteByDan.exe"; Description: "Launch Paste by Dan"; Flags: nowait postinstall skipifsilent
