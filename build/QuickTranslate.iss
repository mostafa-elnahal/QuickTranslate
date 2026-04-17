#ifndef IsSelfContained
#define IsSelfContained "1"
#endif

#define MyAppName "QuickTranslate"
#define MyAppVersion "1.0.0-beta.1"
#define MyAppPublisher "QuickTranslate"
#define MyAppExeName "QuickTranslate.exe"

#if IsSelfContained == "1"
  #define OutputName "QuickTranslate_Setup_Standalone"
  #define SourceDir "..\src\QuickTranslate\bin\Release\net8.0-windows\win-x64\publish_standalone\*"
  #define ExeSource "..\src\QuickTranslate\bin\Release\net8.0-windows\win-x64\publish_standalone\QuickTranslate.exe"
#else
  #define OutputName "QuickTranslate_Setup_Light"
  #define SourceDir "..\src\QuickTranslate\bin\Release\net8.0-windows\win-x64\publish_light\*"
  #define ExeSource "..\src\QuickTranslate\bin\Release\net8.0-windows\win-x64\publish_light\QuickTranslate.exe"
#endif

[Setup]
AppId={{D37D0A21-7A71-4A53-BFE1-817CE4F92BA7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\SetupOutput
OutputBaseFilename={#OutputName}
SetupIconFile=..\src\QuickTranslate\Assets\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Automatically start QuickTranslate on Windows startup"; GroupDescription: "System Integration"

[Files]
Source: "{#ExeSource}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
#if IsSelfContained == "0"
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  // Check for .NET 8 Desktop Runtime (x64) using the precise sharedfx registry key
  if not RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    if MsgBox('QuickTranslate requires the .NET 8.0 Desktop Runtime (x64) to be installed.' + #13#10 +
              'It appears you do not currently have it installed on this system.' + #13#10#13#10 +
              'Would you like to download it now from Microsoft?', mbConfirmation, MB_YESNO) = idYes then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;
#endif
