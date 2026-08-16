#ifndef Channel
  #define Channel "Production"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#if Channel == "Test"
  #define MyAppName "Kanban Task Board (Test)"
  #define MyAppId "18423CD5-03AE-4CED-8914-EFD56BF5CF28"
  #define DataFolderName "KanbanApp.Test"
#else
  #define MyAppName "Kanban Task Board"
  #define MyAppId "4172C30A-F96E-4741-B3A1-B16770195903"
  #define DataFolderName "KanbanApp"
#endif

#define MyAppPublisher "Jeremy Hillier Consulting Inc"
#define MyAppExeName "KanbanApp.exe"

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
AppMutex=KanbanTaskBoard-{#Channel}
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Output
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\publish\{#Channel}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--seed-data-folder ""{code:GetDataFolder}"""; Flags: runhidden nowait skipifsilent; StatusMsg: "Setting up data storage..."

[Code]
var
  DataDirPage: TInputDirWizardPage;

procedure InitializeWizard;
begin
  DataDirPage := CreateInputDirPage(wpSelectDir,
    'Select Task Data Location', 'Where should {#MyAppName} store your task data?',
    'Setup will store your task database, and any screenshots you attach to tasks, in the folder below.' + #13#10 +
    'You can change this later from within the app (Settings > Data Storage).',
    False, '');
  DataDirPage.Add('');
  DataDirPage.Values[0] := ExpandConstant('{localappdata}\{#DataFolderName}');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (DataDirPage <> nil) and (PageID = DataDirPage.ID) then
    Result := FileExists(ExpandConstant('{localappdata}\{#DataFolderName}\config.json'));
end;

function GetDataFolder(Param: string): string;
begin
  Result := DataDirPage.Values[0];
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox('{#MyAppName} has been removed.' + #13#10#13#10 +
      'Your task data was left in place at:' + #13#10 +
      ExpandConstant('{localappdata}\{#DataFolderName}') + #13#10#13#10 +
      'Delete that folder yourself if you no longer need it.',
      mbInformation, MB_OK);
  end;
end;
