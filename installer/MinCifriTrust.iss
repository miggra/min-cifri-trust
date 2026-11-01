#define MyAppName "MinCifri Trust"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CodeCraft"
#define MyAppExeName "MinCifriTrust.exe"
#define MyTaskName "MinCifriTrust"
#define PublishDir "..\artifacts\publish"

[Setup]
AppId={{A8F3C2E1-9B4D-4F6A-8E21-7C5D1B0A9F33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=MinCifriTrust-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.AutostartTask=Запускать при входе в Windows — приложение не надо будет каждый раз запускать самостоятельно
english.AutostartTask=Start at Windows logon — you won't need to launch the app yourself each time
russian.AdditionalOptions=Дополнительные параметры:
english.AdditionalOptions=Additional options:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
procedure CreateAutostartTask;
var
  ResultCode: Integer;
  Params: String;
begin
  Params :=
    '/Create /F /TN "{#MyTaskName}" /TR """' + ExpandConstant('{app}\{#MyAppExeName}') +
    '""" /SC ONLOGON /RL HIGHEST';
  Exec('schtasks.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure DeleteAutostartTask;
var
  ResultCode: Integer;
begin
  Exec('schtasks.exe', '/Delete /F /TN "{#MyTaskName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('autostart') then
      CreateAutostartTask
    else
      DeleteAutostartTask;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    DeleteAutostartTask;
end;
