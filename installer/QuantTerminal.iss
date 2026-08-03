; Inno Setup script for Quant Terminal.
; Built by .github/workflows/release.yml against the win-x64 self-contained publish output;
; PublishDir/MyAppVersion are passed in with /D so this file never needs hand-editing per release.
;
; To build locally: `dotnet publish QuantHub.Desktop -c Release -r win-x64`, then
;   iscc installer/QuantTerminal.iss /DMyAppVersion=1.0.0 /DPublishDir=..\QuantHub.Desktop\bin\Release\net8.0\win-x64\publish

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\QuantHub.Desktop\bin\Release\net8.0\win-x64\publish"
#endif

#define MyAppName "Quant Terminal"
#define MyAppPublisher "Viraaj Vashista"
#define MyAppURL "https://github.com/viraajvashista3-tech/Quant-Hub"
#define MyAppExeName "QuantTerminal.exe"

[Setup]
; Fixed GUID - identifies "Quant Terminal" across every version so upgrades install in place
; instead of Windows treating each release as an unrelated app.
AppId={{49DDB8D1-2F18-48A6-942B-E90E016F810D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=QuantTerminalSetup-{#MyAppVersion}
OutputDir=output
SetupIconFile=..\QuantHub.Desktop\AppIcon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
